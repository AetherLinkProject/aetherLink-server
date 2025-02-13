using Volo.Abp.Settings;

namespace AetherLinkServer.Settings
{
    public class AetherLinkServerSettingDefinitionProvider : SettingDefinitionProvider
    {
        public override void Define(ISettingDefinitionContext context)
        {
            //Define your own settings here. Example:
            //context.Add(new SettingDefinition(AetherLinkServerSettings.MySetting1));
        }
    }
}
