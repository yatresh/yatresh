// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.UpgradeAssistant
{
    public abstract class UpgradeCommand<T> : UpgradeCommand
        where T : notnull
    {
        public T Value { get; init; } = default!;
    }
}
