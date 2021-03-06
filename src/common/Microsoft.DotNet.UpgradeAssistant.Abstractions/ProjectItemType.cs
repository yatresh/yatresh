// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.UpgradeAssistant
{
    public record ProjectItemType(string Name)
    {
        public static ProjectItemType Compile { get; } = new ProjectItemType("Compile");

        public static ProjectItemType Content { get; } = new ProjectItemType("Content");

        public static ProjectItemType EmbeddedResource { get; } = new ProjectItemType("EmbeddedResource");

        public static ProjectItemType None { get; } = new ProjectItemType("None");

        public static ProjectItemType ProjectReference { get; } = new ProjectItemType("ProjectReference");

        public static ProjectItemType Reference { get; } = new ProjectItemType("Reference");

        public override string ToString() => Name;
    }
}
