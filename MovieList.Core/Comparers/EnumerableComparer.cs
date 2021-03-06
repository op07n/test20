using System.Collections.Generic;

namespace MovieList.Comparers
{
    public sealed class EnumerableComparer<T> : IComparer<IEnumerable<T>>
    {
        private readonly IComparer<T> comparer;

        public EnumerableComparer(IComparer<T> comparer)
            => this.comparer = comparer;

        public static EnumerableComparer<T> Default { get; } = new EnumerableComparer<T>(Comparer<T>.Default);

        public int Compare(IEnumerable<T> x, IEnumerable<T> y)
        {
            using var leftEnumerator = x.GetEnumerator();
            using var rightEnumerator = y.GetEnumerator();

            int? result = null;

            while (result is null)
            {
                bool leftPresent = leftEnumerator.MoveNext();
                bool rightPresent = rightEnumerator.MoveNext();

                if (!leftPresent && !rightPresent)
                {
                    result = 0;
                } else if (!leftPresent)
                {
                    result = -1;
                } else if (!rightPresent)
                {
                    result = 1;
                } else
                {
                    int itemResult = this.comparer.Compare(leftEnumerator.Current, rightEnumerator.Current);

                    if (itemResult != 0)
                    {
                        result = itemResult;
                    }
                }
            }

            return result.Value;
        }
    }
}
