// (C) Copyright ETAS 2015.
// Distributed under the Boost Software License, Version 1.0.
// (See accompanying file LICENSE_1_0.txt or copy at
// http://www.boost.org/LICENSE_1_0.txt)

// This file has been modified by Microsoft on 8/2017.

using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Extensions.MSBuild;
using Microsoft.VisualStudio.Workspace.Indexing;
using Microsoft.VisualStudio.Workspace.VSIntegration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DTEProject = EnvDTE.Project;

namespace VisualStudioAdapter
{
    /// <summary>
    /// Base class for DTE2-based Visual Studio instances.
    /// </summary>
    public class VisualStudio : IVisualStudio
    {
        /// <summary>
        /// Constant strings which distinguish Solution item kinds.
        /// </summary>
        private static class EnvDTEProjectKinds
        {
            /// <summary>
            /// Solution folder item kind label
            /// </summary>
            public const string VsProjectKindSolutionFolder = "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}";

            /// <summary>
            /// C++ project item kind label
            /// </summary>
            public const string VsProjectKindVCpp = "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}";
        }

        private DTE2 _dte;
        private ServiceProvider _serviceProvider;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dte">The DTE2 instance for a running Visual Studio instance</param>
        /// <param name="version">The version identifying the Visual Studio instance</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "dte"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "dte"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "dte")]
        public VisualStudio(DTE2 dte)
        {
            this._dte = dte;
            this._serviceProvider = new ServiceProvider((Microsoft.VisualStudio.OLE.Interop.IServiceProvider)_dte);
        }

        #region IVisualStudio

        public async Task<DebuggingProperties> GetDebuggingPropertiesAsync(string binary)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var vsSolution = (IVsSolution)this._serviceProvider.GetService(typeof(SVsSolution));
            if (vsSolution == null)
            {
                return null;
            }

            IEnumHierarchies enumHierarchies;
            Guid guid = Guid.Empty;
            var hr = vsSolution.GetProjectEnum((uint)__VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION, ref guid, out enumHierarchies);
            if (hr != VSConstants.S_OK || enumHierarchies == null)
            {
                return null;
            }

            List<IVsHierarchy> deferredHierarchies = new List<IVsHierarchy>();

            IVsHierarchy[] hierarchies = new IVsHierarchy[1];
            uint fetched;
            while (enumHierarchies.Next(1, hierarchies, out fetched) == VSConstants.S_OK && fetched > 0)
            {
                var hierarchy = hierarchies[0];
                if (hierarchy == null)
                {
                    continue;
                }

                if (IsInDeferredState(hierarchy))
                {
                    deferredHierarchies.Add(hierarchy);
                }
                else
                {
                    var debuggingProperties = GetDebugPropsIfMatching(hierarchy, binary);
                    if (debuggingProperties != null)
                    {
                        return debuggingProperties;
                    }
                }
            }

            // If binary was not found in loaded hierarchies, fall back to searching in deferred hierarchies.
            if (deferredHierarchies.Count > 0)
            {
                var workspaceService = (IVsSolutionWorkspaceService)this._serviceProvider.GetService(typeof(SVsSolutionWorkspaceService));
                var indexService = workspaceService.CurrentWorkspace.GetIndexWorkspaceService();
                var solutionService = workspaceService.CurrentWorkspace.GetService<ISolutionService>();

                var solutionPath = workspaceService.SolutionFile;
                var solutionConfig = (SolutionConfiguration2)this._dte.Solution.SolutionBuild.ActiveConfiguration;
                var solutionDir = Path.GetDirectoryName(solutionPath);
                var solutionContext = $"{solutionConfig.Name}|{solutionConfig.PlatformName}";

                foreach (var hierarchy in deferredHierarchies)
                {
                    var projectPath = GetProjectPath(hierarchy);
                    if (projectPath == null)
                    {
                        continue;
                    }

                    var isMatch = await ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                    {
                        var projectContext = await solutionService.GetProjectConfigurationAsync(solutionPath, projectPath, solutionContext);
                        var outputs = await indexService.GetFileReferencesAsync(projectPath, refreshOption: true, context: projectContext,
                            referenceTypes: (int)FileReferenceInfoType.Output);
                        return outputs.Select(f => workspaceService.CurrentWorkspace.MakeRooted(f.Path)).Contains(binary);
                    });

                    if (isMatch)
                    {
                        var loadedProject = EnsureProjectIsLoaded(hierarchy, vsSolution);
                        return GetDebugPropsIfMatching(loadedProject, binary);
                    }
                }
            }

            return null;
        }

        #endregion IVisualStudio

        private static bool IsInDeferredState(IVsHierarchy hierarchy)
        {
            object deferred;
            var hr = hierarchy.GetProperty((uint)VSConstants.VSITEMID.Root, (int)__VSHPROPID9.VSHPROPID_IsDeferred, out deferred);
            if (hr != VSConstants.S_OK)
            {
                return false;
            }
            return (bool)deferred;
        }

        private static string GetProjectPath(IVsHierarchy hierarchy)
        {
            string name;
            var hr = hierarchy.GetCanonicalName((uint)VSConstants.VSITEMID.Root, out name);
            if (hr != VSConstants.S_OK)
            {
                return null;
            }
            return name;
        }

        private static IVsHierarchy EnsureProjectIsLoaded(IVsHierarchy hierarchy, IVsSolution vsSolution)
        {
            Guid projectGuid;
            var hr = hierarchy.GetGuidProperty((uint)VSConstants.VSITEMID.Root, (int)__VSHPROPID.VSHPROPID_ProjectIDGuid, out projectGuid);
            if (hr != VSConstants.S_OK)
            {
                return null;
            }

            hr = ((IVsSolution4)vsSolution).EnsureProjectIsLoaded(projectGuid, (uint)__VSBSLFLAGS.VSBSLFLAGS_None);
            if (hr != VSConstants.S_OK)
            {
                return null;
            }

            IVsHierarchy loadedProject;
            hr = vsSolution.GetProjectOfGuid(projectGuid, out loadedProject);
            if (hr != VSConstants.S_OK)
            {
                return null;
            }
            return loadedProject;
        }

        private static DebuggingProperties GetDebugPropsIfMatching(IVsHierarchy hierarchy, string binary)
        {
            object extObject;
            hierarchy.GetProperty((uint)VSConstants.VSITEMID.Root, (int)__VSHPROPID.VSHPROPID_ExtObject, out extObject);
            var dteProject = (DTEProject)extObject;

            if (dteProject.Kind != EnvDTEProjectKinds.VsProjectKindVCpp)
            {
                return null;
            }

            var project = new Project(dteProject);
            var configuration = project.ActiveConfiguration;
            if (!string.Equals(binary, configuration.PrimaryOutput, StringComparison.Ordinal))
            {
                return null;
            }

            return new DebuggingProperties
            {
                Environment = configuration.VSDebugConfiguration.Environment,
                WorkingDirectory = configuration.VSDebugConfiguration.WorkingDirectory
            };
        }
    }
}
