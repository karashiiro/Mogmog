using System.Collections.Generic;
using System.Linq;

namespace Mogmog.Tests
{
    public static class TestUtils
    {
        public static bool ElementsEqual<T>(this IList<T> me, IList<T> test) where T : class
        {
            if (me.Count() != test.Count())
                return false;
            for (var i = 0; i < me.Count(); i++)
            {
                if (me[i] != test[i])
                    return false;
            }
            return true;
        }
    }
}
