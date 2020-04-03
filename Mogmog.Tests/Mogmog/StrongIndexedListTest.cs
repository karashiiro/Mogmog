using Mogmog.Tests;
using NUnit.Framework;
using System;

namespace Mogmog
{
    [TestFixture]
    public class StrongIndexedListTests
    {
        private StrongIndexedList<string> list;

        [SetUp]
        public void Setup()
        {
            list = new StrongIndexedList<string>();
        }

        [Test]
        public void Add_ShouldAddSequentially()
        {
            string[] test = new string[] { "1", "2", "3", "4", "5" };
            foreach (string t in test)
            {
                list.Add(t);
            }
            Assert.IsTrue(test.ElementsEqual(list), "Expected {0}, got {1}.", string.Join(",", test), string.Join(",", list));
        }

        [Test]
        public void Add_ShouldFillNull()
        {
            string[] seed = new string[] { "1", "2", "3", "4", "5" };
            foreach (string s in seed)
            {
                list.Add(s);
            }
            list.Remove("3");
            list.Add("6");
            string[] expectedResult = new string[] { "1", "2", "6", "4", "5" };
            Assert.IsTrue(expectedResult.ElementsEqual(list), "Expected {0}, got {1}.", string.Join(",", expectedResult), string.Join(",", list));
        }

        [Test]
        [TestCase("1", true)]
        [TestCase("2", true)]
        [TestCase("3", true)]
        [TestCase("4", true)]
        [TestCase("5", false)] // This is at the end, so the list should be resized.
        public void Remove_ShouldReplaceWithNull(string value, bool shouldSucceed)
        {
            string[] test = new string[] { "1", "2", "3", "4", "5" };
            foreach (string t in test)
            {
                list.Add(t);
            }
            var idx = Array.IndexOf(test, value);
            test[idx] = null;
            list.Remove(value);
            Assert.IsTrue(test.ElementsEqual(list) == shouldSucceed, "Expected {0}, got {1}.", string.Join(",", test), string.Join(",", list));
        }

        [Test]
        public void Remove_ShouldResizeIfEndRemoved()
        {
            string[] test = new string[] { "1", "2", "3", "4", "5" };
            foreach (string t in test)
            {
                list.Add(t);
            }
            string[] expectedResult = new string[] { "1", "2", "3", "4" };
            list.Remove("5");
            Assert.IsTrue(expectedResult.ElementsEqual(list), "Expected {0}, got {1}.", string.Join(",", expectedResult), string.Join(",", list));
        }

        [Test]
        [TestCase("1", true)]
        [TestCase("2", true)]
        [TestCase("3", true)]
        [TestCase("4", true)]
        [TestCase("5", false)] // This is at the end, so the list should be resized.
        public void RemoveAt_ShouldReplaceWithNull(string value, bool shouldSucceed)
        {
            string[] test = new string[] { "1", "2", "3", "4", "5" };
            foreach (string t in test)
            {
                list.Add(t);
            }
            var idx = Array.IndexOf(test, value);
            test[idx] = null;
            list.RemoveAt(idx);
            Assert.IsTrue(test.ElementsEqual(list) == shouldSucceed, "Expected {0}, got {1}.", string.Join(",", test), string.Join(",", list));
        }

        [Test]
        public void RemoveAt_ShouldResizeIfEndRemoved()
        {
            string[] test = new string[] { "1", "2", "3", "4", "5" };
            foreach (string t in test)
            {
                list.Add(t);
            }
            string[] expectedResult = new string[] { "1", "2", "3", "4" };
            list.RemoveAt(4);
            Assert.IsTrue(expectedResult.ElementsEqual(list), "Expected {0}, got {1}.", string.Join(",", expectedResult), string.Join(",", list));
        }
    }
}