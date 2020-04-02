using Mogmog.FFXIV.UpgradeLayer;
using NUnit.Framework;

namespace Mogmog.FFXIV.Tests
{
    [TestFixture]
    public class DisposableStrongIndexedListTests
    {
        private DisposableStrongIndexedList<TestDisposable> list;

        [SetUp]
        public void Setup()
        {
            list = new DisposableStrongIndexedList<TestDisposable>();
        }

        [Test]
        public void Remove_DisposesObject()
        {
            TestDisposable[] objects = new TestDisposable[10];
            for (var i = 0; i < 10; i++)
            {
                objects[i] = new TestDisposable();
                list.Add(objects[i]);
            }
            Assert.IsFalse(objects[0].IsDisposed, "Expected {0}, got {1}.", false, objects[0].IsDisposed);
            list.Remove(objects[0]);
            Assert.IsTrue(objects[0].IsDisposed, "Expected {0}, got {1}.", true, objects[0].IsDisposed);
        }

        [Test]
        public void RemoveAt_DisposesObject()
        {
            TestDisposable[] objects = new TestDisposable[10];
            for (var i = 0; i < 10; i++)
            {
                objects[i] = new TestDisposable();
                list.Add(objects[i]);
            }
            Assert.IsFalse(objects[0].IsDisposed, "Expected {0}, got {1}.", false, objects[0].IsDisposed);
            list.RemoveAt(0);
            Assert.IsTrue(objects[0].IsDisposed, "Expected {0}, got {1}.", true, objects[0].IsDisposed);
        }

        [Test]
        public void Dispose_DisposesAllObjects()
        {
            TestDisposable[] objects = new TestDisposable[10];
            for (var i = 0; i < 10; i++)
            {
                objects[i] = new TestDisposable();
                list.Add(objects[i]);
            }
            foreach (var obj in objects)
            {
                Assert.IsFalse(obj.IsDisposed, "Expected {0}, got {1}.", false, obj.IsDisposed);
            }
            list.Dispose();
            foreach (var obj in objects)
            {
                Assert.IsTrue(obj.IsDisposed, "Expected {0}, got {1}.", true, obj.IsDisposed);
            }
        }
    }
}
