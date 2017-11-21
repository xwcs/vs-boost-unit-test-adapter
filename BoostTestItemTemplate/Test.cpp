#define BOOST_TEST_MODULE mytests
#include <boost/test/included/unit_test.hpp>

BOOST_AUTO_TEST_CASE(myTestCase)
{
  BOOST_CHECK_EQUAL(1, 1);
  BOOST_CHECK(true);
}