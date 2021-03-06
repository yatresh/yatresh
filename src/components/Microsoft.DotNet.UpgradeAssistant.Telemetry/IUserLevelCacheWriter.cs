// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.DotNet.UpgradeAssistant.Telemetry
{
    public interface IUserLevelCacheWriter
    {
        string RunWithCache(string cacheKey, Func<string> getValueToCache);
    }
}
