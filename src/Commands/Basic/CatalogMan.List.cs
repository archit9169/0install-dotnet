// Copyright Bastian Eicher et al.
// Licensed under the GNU Lesser Public License

using ZeroInstall.Commands.Properties;

namespace ZeroInstall.Commands.Basic
{
    partial class CatalogMan
    {
        private class List : CatalogSubCommand
        {
            #region Metadata
            // ReSharper disable once MemberHidesStaticFromOuterClass
            public const string Name = "list";

            public override string Description => Resources.DescriptionCatalogList;

            public override string Usage => "";

            protected override int AdditionalArgsMax => 0;

            public List(ICommandHandler handler)
                : base(handler)
            {}
            #endregion

            public override ExitCode Execute()
            {
                Handler.Output(Resources.CatalogSources, Services.Feeds.CatalogManager.GetSources());
                return ExitCode.OK;
            }
        }
    }
}
