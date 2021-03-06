using System.Configuration;

using Composite.Core.Configuration;

using Microsoft.Practices.EnterpriseLibrary.Common.Configuration;


namespace Composite.Functions.Plugins.WidgetFunctionProvider.Runtime
{
    internal sealed class WidgetFunctionProviderSettings : SerializableConfigurationSection
    {
        public const string SectionName = "Composite.Functions.Plugins.WidgetFunctionProviderConfiguration";


        private const string _dataProviderPluginsProperty = "WidgetFunctionProviderPlugins";
        [ConfigurationProperty(_dataProviderPluginsProperty)]
        public NameTypeManagerTypeConfigurationElementCollection<WidgetFunctionProviderData> WidgetFunctionProviderPlugins
        {
            get
            {
                return (NameTypeManagerTypeConfigurationElementCollection<WidgetFunctionProviderData>)base[_dataProviderPluginsProperty];
            }
        }
    }
}
