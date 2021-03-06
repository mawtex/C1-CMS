using System;
using System.Xml;
using System.Workflow.Activities;
using System.Collections.Generic;

using Composite.C1Console.Actions;
using Composite.C1Console.Forms.Flows;
using Composite.Data.Validation.ClientValidationRules;


namespace Composite.C1Console.Workflow
{
    [ExternalDataExchange()]
    internal interface IFormsWorkflowActivityService
    {
        void DeliverFormData(Guid instanceId, string containerLabel, IFlowUiContainerType containerType, string formMarkup, Dictionary<string, object> bindings);
        void DeliverFormData(Guid instanceId, string containerLabel, IFlowUiContainerType containerType, IFormMarkupProvider formMarkupProvider, Dictionary<string, object> bindings);
        void DeliverFormData(Guid instanceId, string containerLabel, IFlowUiContainerType containerType, string formMarkup, Dictionary<string, object> bindings, Dictionary<string, List<ClientValidationRule>> bindingsValidationRules);
        void DeliverFormData(Guid instanceId, string containerLabel, IFlowUiContainerType containerType, IFormMarkupProvider formMarkupProvider, Dictionary<string, object> bindings, Dictionary<string, List<ClientValidationRule>> bindingsValidationRules);

        void DeliverCustomToolbarDefinition(Guid instanceId, string customToolbarMarkup);
        void DeliverCustomToolbarDefinition(Guid instanceId, IFormMarkupProvider customToolbarMarkupProvider);

        FlowControllerServicesContainer GetFlowServicesContainer(Guid instanceId);
    }
}
