namespace TextEngine
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Resolves player-typed noun phrases against named collections: exact
    /// match first, then names starting with the phrase, then names containing
    /// it. When the best tier holds more than one distinct name the result is
    /// ambiguous and the caller receives the candidate names to ask about.
    /// Pure and stateless so it is unit-testable.
    /// </summary>
    public static class NounMatcher
    {
        /// <summary>
        /// Returns the best match for the phrase, or default when nothing
        /// matches or the phrase is ambiguous. On ambiguity,
        /// <paramref name="ambiguousNames"/> is set to the distinct candidate
        /// names (otherwise null).
        /// </summary>
        public static T Find<T>(IEnumerable<T> source, Func<T, string> nameOf, string nounPhrase, out List<string> ambiguousNames)
        {
            ambiguousNames = null;
            if (string.IsNullOrEmpty(nounPhrase) || source == null) return default;
            string phrase = nounPhrase.ToLower();
            var named = source.Where(x => x != null && !string.IsNullOrEmpty(nameOf(x))).ToList();
            var matches = named.Where(x => nameOf(x).ToLower() == phrase).ToList();
            if (matches.Count == 0)
                matches = named.Where(x => nameOf(x).ToLower().StartsWith(phrase)).ToList();
            if (matches.Count == 0)
                matches = named.Where(x => nameOf(x).ToLower().Contains(phrase)).ToList();
            if (matches.Count == 0) return default;
            var distinctNames = matches.Select(nameOf).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (distinctNames.Count > 1)
            {
                ambiguousNames = distinctNames;
                return default;
            }
            return matches[0];
        }
    }
}
