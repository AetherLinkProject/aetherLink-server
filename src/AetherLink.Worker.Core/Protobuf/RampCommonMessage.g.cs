// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: ramp_common_message.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Ramp {

  /// <summary>Holder for reflection information generated from ramp_common_message.proto</summary>
  public static partial class RampCommonMessageReflection {

    #region Descriptor
    /// <summary>File descriptor for ramp_common_message.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static RampCommonMessageReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "ChlyYW1wX2NvbW1vbl9tZXNzYWdlLnByb3RvEgRyYW1wGg9hZWxmL2NvcmUu",
            "cHJvdG8aEmFlbGYvb3B0aW9ucy5wcm90bxoLYWNzMTIucHJvdG8aG2dvb2ds",
            "ZS9wcm90b2J1Zi9lbXB0eS5wcm90bxoeZ29vZ2xlL3Byb3RvYnVmL3dyYXBw",
            "ZXJzLnByb3RvGh9nb29nbGUvcHJvdG9idWYvdGltZXN0YW1wLnByb3RvIpUB",
            "CgtUb2tlbkFtb3VudBIPCgdzd2FwX2lkGAEgASgJEhcKD3RhcmdldF9jaGFp",
            "bl9pZBgCIAEoAxIfChd0YXJnZXRfY29udHJhY3RfYWRkcmVzcxgDIAEoCRIV",
            "Cg10b2tlbl9hZGRyZXNzGAQgASgJEhQKDG9yaWdpbl90b2tlbhgFIAEoCRIO",
            "CgZhbW91bnQYBiABKANiBnByb3RvMw=="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::AElf.Types.CoreReflection.Descriptor, global::AElf.OptionsReflection.Descriptor, global::AElf.Standards.ACS12.Acs12Reflection.Descriptor, global::Google.Protobuf.WellKnownTypes.EmptyReflection.Descriptor, global::Google.Protobuf.WellKnownTypes.WrappersReflection.Descriptor, global::Google.Protobuf.WellKnownTypes.TimestampReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::Ramp.TokenAmount), global::Ramp.TokenAmount.Parser, new[]{ "SwapId", "TargetChainId", "TargetContractAddress", "TokenAddress", "OriginToken", "Amount" }, null, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  public sealed partial class TokenAmount : pb::IMessage<TokenAmount>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<TokenAmount> _parser = new pb::MessageParser<TokenAmount>(() => new TokenAmount());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<TokenAmount> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Ramp.RampCommonMessageReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public TokenAmount() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public TokenAmount(TokenAmount other) : this() {
      swapId_ = other.swapId_;
      targetChainId_ = other.targetChainId_;
      targetContractAddress_ = other.targetContractAddress_;
      tokenAddress_ = other.tokenAddress_;
      originToken_ = other.originToken_;
      amount_ = other.amount_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public TokenAmount Clone() {
      return new TokenAmount(this);
    }

    /// <summary>Field number for the "swap_id" field.</summary>
    public const int SwapIdFieldNumber = 1;
    private string swapId_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string SwapId {
      get { return swapId_; }
      set {
        swapId_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "target_chain_id" field.</summary>
    public const int TargetChainIdFieldNumber = 2;
    private long targetChainId_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public long TargetChainId {
      get { return targetChainId_; }
      set {
        targetChainId_ = value;
      }
    }

    /// <summary>Field number for the "target_contract_address" field.</summary>
    public const int TargetContractAddressFieldNumber = 3;
    private string targetContractAddress_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string TargetContractAddress {
      get { return targetContractAddress_; }
      set {
        targetContractAddress_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "token_address" field.</summary>
    public const int TokenAddressFieldNumber = 4;
    private string tokenAddress_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string TokenAddress {
      get { return tokenAddress_; }
      set {
        tokenAddress_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "origin_token" field.</summary>
    public const int OriginTokenFieldNumber = 5;
    private string originToken_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public string OriginToken {
      get { return originToken_; }
      set {
        originToken_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "amount" field.</summary>
    public const int AmountFieldNumber = 6;
    private long amount_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public long Amount {
      get { return amount_; }
      set {
        amount_ = value;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as TokenAmount);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(TokenAmount other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (SwapId != other.SwapId) return false;
      if (TargetChainId != other.TargetChainId) return false;
      if (TargetContractAddress != other.TargetContractAddress) return false;
      if (TokenAddress != other.TokenAddress) return false;
      if (OriginToken != other.OriginToken) return false;
      if (Amount != other.Amount) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (SwapId.Length != 0) hash ^= SwapId.GetHashCode();
      if (TargetChainId != 0L) hash ^= TargetChainId.GetHashCode();
      if (TargetContractAddress.Length != 0) hash ^= TargetContractAddress.GetHashCode();
      if (TokenAddress.Length != 0) hash ^= TokenAddress.GetHashCode();
      if (OriginToken.Length != 0) hash ^= OriginToken.GetHashCode();
      if (Amount != 0L) hash ^= Amount.GetHashCode();
      if (_unknownFields != null) {
        hash ^= _unknownFields.GetHashCode();
      }
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void WriteTo(pb::CodedOutputStream output) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      output.WriteRawMessage(this);
    #else
      if (SwapId.Length != 0) {
        output.WriteRawTag(10);
        output.WriteString(SwapId);
      }
      if (TargetChainId != 0L) {
        output.WriteRawTag(16);
        output.WriteInt64(TargetChainId);
      }
      if (TargetContractAddress.Length != 0) {
        output.WriteRawTag(26);
        output.WriteString(TargetContractAddress);
      }
      if (TokenAddress.Length != 0) {
        output.WriteRawTag(34);
        output.WriteString(TokenAddress);
      }
      if (OriginToken.Length != 0) {
        output.WriteRawTag(42);
        output.WriteString(OriginToken);
      }
      if (Amount != 0L) {
        output.WriteRawTag(48);
        output.WriteInt64(Amount);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalWriteTo(ref pb::WriteContext output) {
      if (SwapId.Length != 0) {
        output.WriteRawTag(10);
        output.WriteString(SwapId);
      }
      if (TargetChainId != 0L) {
        output.WriteRawTag(16);
        output.WriteInt64(TargetChainId);
      }
      if (TargetContractAddress.Length != 0) {
        output.WriteRawTag(26);
        output.WriteString(TargetContractAddress);
      }
      if (TokenAddress.Length != 0) {
        output.WriteRawTag(34);
        output.WriteString(TokenAddress);
      }
      if (OriginToken.Length != 0) {
        output.WriteRawTag(42);
        output.WriteString(OriginToken);
      }
      if (Amount != 0L) {
        output.WriteRawTag(48);
        output.WriteInt64(Amount);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(ref output);
      }
    }
    #endif

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public int CalculateSize() {
      int size = 0;
      if (SwapId.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(SwapId);
      }
      if (TargetChainId != 0L) {
        size += 1 + pb::CodedOutputStream.ComputeInt64Size(TargetChainId);
      }
      if (TargetContractAddress.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(TargetContractAddress);
      }
      if (TokenAddress.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(TokenAddress);
      }
      if (OriginToken.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(OriginToken);
      }
      if (Amount != 0L) {
        size += 1 + pb::CodedOutputStream.ComputeInt64Size(Amount);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(TokenAmount other) {
      if (other == null) {
        return;
      }
      if (other.SwapId.Length != 0) {
        SwapId = other.SwapId;
      }
      if (other.TargetChainId != 0L) {
        TargetChainId = other.TargetChainId;
      }
      if (other.TargetContractAddress.Length != 0) {
        TargetContractAddress = other.TargetContractAddress;
      }
      if (other.TokenAddress.Length != 0) {
        TokenAddress = other.TokenAddress;
      }
      if (other.OriginToken.Length != 0) {
        OriginToken = other.OriginToken;
      }
      if (other.Amount != 0L) {
        Amount = other.Amount;
      }
      _unknownFields = pb::UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(pb::CodedInputStream input) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      input.ReadRawMessage(this);
    #else
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input);
            break;
          case 10: {
            SwapId = input.ReadString();
            break;
          }
          case 16: {
            TargetChainId = input.ReadInt64();
            break;
          }
          case 26: {
            TargetContractAddress = input.ReadString();
            break;
          }
          case 34: {
            TokenAddress = input.ReadString();
            break;
          }
          case 42: {
            OriginToken = input.ReadString();
            break;
          }
          case 48: {
            Amount = input.ReadInt64();
            break;
          }
        }
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalMergeFrom(ref pb::ParseContext input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
            break;
          case 10: {
            SwapId = input.ReadString();
            break;
          }
          case 16: {
            TargetChainId = input.ReadInt64();
            break;
          }
          case 26: {
            TargetContractAddress = input.ReadString();
            break;
          }
          case 34: {
            TokenAddress = input.ReadString();
            break;
          }
          case 42: {
            OriginToken = input.ReadString();
            break;
          }
          case 48: {
            Amount = input.ReadInt64();
            break;
          }
        }
      }
    }
    #endif

  }

  #endregion

}

#endregion Designer generated code
