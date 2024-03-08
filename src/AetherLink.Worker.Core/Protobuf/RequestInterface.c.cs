// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: request_interface.proto
// </auto-generated>
// Original file comments:
// the version of the language, use proto3 for contracts
#pragma warning disable 0414, 1591
#region Designer generated code

using System.Collections.Generic;
using aelf = global::AElf.CSharp.Core;

namespace AetherLink.Contracts.Consumer {

  #region Events
  #endregion
  /// <summary>
  /// the contract definition: a gRPC service definition.
  /// </summary>
  internal static partial class RequestInterfaceContainer
  {
    static readonly string __ServiceName = "oracle.RequestInterface";

    #region Marshallers
    static readonly aelf::Marshaller<global::AetherLink.Contracts.Consumer.StartOracleRequestInput> __Marshaller_oracle_StartOracleRequestInput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::AetherLink.Contracts.Consumer.StartOracleRequestInput.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::Google.Protobuf.WellKnownTypes.Empty> __Marshaller_google_protobuf_Empty = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::Google.Protobuf.WellKnownTypes.Empty.Parser.ParseFrom);
    static readonly aelf::Marshaller<global::AetherLink.Contracts.Consumer.HandleOracleFulfillmentInput> __Marshaller_oracle_HandleOracleFulfillmentInput = aelf::Marshallers.Create((arg) => global::Google.Protobuf.MessageExtensions.ToByteArray(arg), global::AetherLink.Contracts.Consumer.HandleOracleFulfillmentInput.Parser.ParseFrom);
    #endregion

    #region Methods
    static readonly aelf::Method<global::AetherLink.Contracts.Consumer.StartOracleRequestInput, global::Google.Protobuf.WellKnownTypes.Empty> __Method_StartOracleRequest = new aelf::Method<global::AetherLink.Contracts.Consumer.StartOracleRequestInput, global::Google.Protobuf.WellKnownTypes.Empty>(
        aelf::MethodType.Action,
        __ServiceName,
        "StartOracleRequest",
        __Marshaller_oracle_StartOracleRequestInput,
        __Marshaller_google_protobuf_Empty);

    static readonly aelf::Method<global::AetherLink.Contracts.Consumer.HandleOracleFulfillmentInput, global::Google.Protobuf.WellKnownTypes.Empty> __Method_HandleOracleFulfillment = new aelf::Method<global::AetherLink.Contracts.Consumer.HandleOracleFulfillmentInput, global::Google.Protobuf.WellKnownTypes.Empty>(
        aelf::MethodType.Action,
        __ServiceName,
        "HandleOracleFulfillment",
        __Marshaller_oracle_HandleOracleFulfillmentInput,
        __Marshaller_google_protobuf_Empty);

    #endregion

    #region Descriptors
    public static global::Google.Protobuf.Reflection.ServiceDescriptor Descriptor
    {
      get { return global::AetherLink.Contracts.Consumer.RequestInterfaceReflection.Descriptor.Services[0]; }
    }

    public static global::System.Collections.Generic.IReadOnlyList<global::Google.Protobuf.Reflection.ServiceDescriptor> Descriptors
    {
      get
      {
        return new global::System.Collections.Generic.List<global::Google.Protobuf.Reflection.ServiceDescriptor>()
        {
          global::AetherLink.Contracts.Consumer.RequestInterfaceReflection.Descriptor.Services[0],
        };
      }
    }
    #endregion

    public class RequestInterfaceReferenceState : global::AElf.Sdk.CSharp.State.ContractReferenceState
    {
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::AetherLink.Contracts.Consumer.StartOracleRequestInput, global::Google.Protobuf.WellKnownTypes.Empty> StartOracleRequest { get; set; }
      internal global::AElf.Sdk.CSharp.State.MethodReference<global::AetherLink.Contracts.Consumer.HandleOracleFulfillmentInput, global::Google.Protobuf.WellKnownTypes.Empty> HandleOracleFulfillment { get; set; }
    }
  }
}
#endregion

