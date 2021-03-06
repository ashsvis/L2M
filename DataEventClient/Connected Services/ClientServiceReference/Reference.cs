﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace DataEventClient.ClientServiceReference {
    
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ServiceModel.ServiceContractAttribute(ConfigurationName="ClientServiceReference.IClientEventService", CallbackContract=typeof(DataEventClient.ClientServiceReference.IClientEventServiceCallback))]
    public interface IClientEventService {
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="http://tempuri.org/IClientEventService/RegisterForUpdates")]
        void RegisterForUpdates(System.Guid clientId, string[] categories);
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="http://tempuri.org/IClientEventService/RegisterForUpdates")]
        System.Threading.Tasks.Task RegisterForUpdatesAsync(System.Guid clientId, string[] categories);
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="http://tempuri.org/IClientEventService/UpdateProperty")]
        void UpdateProperty(System.Guid clientId, string category, string pointname, string propname, string value, bool nocash);
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="http://tempuri.org/IClientEventService/UpdateProperty")]
        System.Threading.Tasks.Task UpdatePropertyAsync(System.Guid clientId, string category, string pointname, string propname, string value, bool nocash);
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="http://tempuri.org/IClientEventService/Disconnect")]
        void Disconnect(System.Guid clientId);
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="http://tempuri.org/IClientEventService/Disconnect")]
        System.Threading.Tasks.Task DisconnectAsync(System.Guid clientId);
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="http://tempuri.org/IClientEventService/SubscribeValues")]
        void SubscribeValues(System.Guid clientId);
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="http://tempuri.org/IClientEventService/SubscribeValues")]
        System.Threading.Tasks.Task SubscribeValuesAsync(System.Guid clientId);
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public interface IClientEventServiceCallback {
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="http://tempuri.org/IClientEventService/PropertyUpdated")]
        void PropertyUpdated(System.DateTime servertime, string category, string pointname, string propname, string value);
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public interface IClientEventServiceChannel : DataEventClient.ClientServiceReference.IClientEventService, System.ServiceModel.IClientChannel {
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public partial class ClientEventServiceClient : System.ServiceModel.DuplexClientBase<DataEventClient.ClientServiceReference.IClientEventService>, DataEventClient.ClientServiceReference.IClientEventService {
        
        public ClientEventServiceClient(System.ServiceModel.InstanceContext callbackInstance) : 
                base(callbackInstance) {
        }
        
        public ClientEventServiceClient(System.ServiceModel.InstanceContext callbackInstance, string endpointConfigurationName) : 
                base(callbackInstance, endpointConfigurationName) {
        }
        
        public ClientEventServiceClient(System.ServiceModel.InstanceContext callbackInstance, string endpointConfigurationName, string remoteAddress) : 
                base(callbackInstance, endpointConfigurationName, remoteAddress) {
        }
        
        public ClientEventServiceClient(System.ServiceModel.InstanceContext callbackInstance, string endpointConfigurationName, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(callbackInstance, endpointConfigurationName, remoteAddress) {
        }
        
        public ClientEventServiceClient(System.ServiceModel.InstanceContext callbackInstance, System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(callbackInstance, binding, remoteAddress) {
        }
        
        public void RegisterForUpdates(System.Guid clientId, string[] categories) {
            base.Channel.RegisterForUpdates(clientId, categories);
        }
        
        public System.Threading.Tasks.Task RegisterForUpdatesAsync(System.Guid clientId, string[] categories) {
            return base.Channel.RegisterForUpdatesAsync(clientId, categories);
        }
        
        public void UpdateProperty(System.Guid clientId, string category, string pointname, string propname, string value, bool nocash) {
            base.Channel.UpdateProperty(clientId, category, pointname, propname, value, nocash);
        }
        
        public System.Threading.Tasks.Task UpdatePropertyAsync(System.Guid clientId, string category, string pointname, string propname, string value, bool nocash) {
            return base.Channel.UpdatePropertyAsync(clientId, category, pointname, propname, value, nocash);
        }
        
        public void Disconnect(System.Guid clientId) {
            base.Channel.Disconnect(clientId);
        }
        
        public System.Threading.Tasks.Task DisconnectAsync(System.Guid clientId) {
            return base.Channel.DisconnectAsync(clientId);
        }
        
        public void SubscribeValues(System.Guid clientId) {
            base.Channel.SubscribeValues(clientId);
        }
        
        public System.Threading.Tasks.Task SubscribeValuesAsync(System.Guid clientId) {
            return base.Channel.SubscribeValuesAsync(clientId);
        }
    }
}
