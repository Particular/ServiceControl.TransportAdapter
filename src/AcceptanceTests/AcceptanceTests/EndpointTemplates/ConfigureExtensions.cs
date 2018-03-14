﻿namespace NServiceBus.AcceptanceTests.EndpointTemplates
{
    using System.Threading.Tasks;
    using AcceptanceTesting.Support;
    using ObjectBuilder;

    public static class ConfigureExtensions
    {
        public static async Task DefineTransport(this EndpointConfiguration config, RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomizationConfiguration)
        {
            var transportConfiguration = new ConfigureEndpointLearningTransport();
            await transportConfiguration.Configure(config);
            runDescriptor.OnTestCompleted(_ => transportConfiguration.Cleanup());
        }

        public static void RegisterComponentsAndInheritanceHierarchy(this EndpointConfiguration builder, RunDescriptor runDescriptor)
        {
            builder.RegisterComponents(r => { RegisterInheritanceHierarchyOfContextOnContainer(runDescriptor, r); });
        }

        static void RegisterInheritanceHierarchyOfContextOnContainer(RunDescriptor runDescriptor, IConfigureComponents r)
        {
            var type = runDescriptor.ScenarioContext.GetType();
            while (type != typeof(object))
            {
                r.RegisterSingleton(type, runDescriptor.ScenarioContext);
                type = type.BaseType;
            }
        }
    }
}