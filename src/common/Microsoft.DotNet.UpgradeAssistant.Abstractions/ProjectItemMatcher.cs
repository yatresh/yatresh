// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.UpgradeAssistant
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "ProjectItemMatcher instances are not likely to be compared, so the default equals behavior is sufficient")]
    public readonly struct ProjectItemMatcher
    {
        private readonly object _match;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectItemMatcher"/> struct that
        /// matches using regex using just the filename of the input.
        /// </summary>
        /// <param name="regex">The regular expression to be used to match.</param>
        public ProjectItemMatcher(Regex regex)
        {
            _match = regex;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectItemMatcher"/> struct that
        /// matches with the given string against the end of the input.
        /// </summary>
        /// <param name="match">The string to be used to match.</param>
        public ProjectItemMatcher(string match)
        {
            _match = match;
        }

        public bool Match(string input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            return _match switch
            {
                Regex regex => regex.IsMatch(Path.GetFileName(input)),
                string str => input.EndsWith(str, StringComparison.OrdinalIgnoreCase),
                _ => throw new NotImplementedException(),
            };
        }

        public static implicit operator ProjectItemMatcher(Regex regex)
            => new(regex);

        public static implicit operator ProjectItemMatcher(string str)
            => new(str);
    }
}
