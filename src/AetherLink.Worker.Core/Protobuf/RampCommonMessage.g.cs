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
            "ZXJzLnByb3RvGh9nb29nbGUvcHJvdG9idWYvdGltZXN0YW1wLnByb3RvInsK",
            "FVRva2VuVHJhbnNmZXJNZXRhZGF0YRIXCg90YXJnZXRfY2hhaW5faWQYASAB",
            "KAMSFQoNdG9rZW5fYWRkcmVzcxgCIAEoCRIOCgZzeW1ib2wYAyABKAkSDgoG",
            "YW1vdW50GAQgASgDEhIKCmV4dHJhX2RhdGEYBSABKAxiBnByb3RvMw=="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::AElf.Types.CoreReflection.Descriptor, global::AElf.OptionsReflection.Descriptor, global::AElf.Standards.ACS12.Acs12Reflection.Descriptor, global::Google.Protobuf.WellKnownTypes.EmptyReflection.Descriptor, global::Google.Protobuf.WellKnownTypes.WrappersReflection.Descriptor, global::Google.Protobuf.WellKnownTypes.TimestampReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::Ramp.TokenTransferMetadata), global::Ramp.TokenTransferMetadata.Parser, new[]{ "TargetChainId", "TokenAddress", "Symbol", "Amount", "ExtraData" }, null, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  public sealed partial class TokenTransferMetadata : pb::IMessage<TokenTransferMetadata> {
    private static readonly pb::MessageParser<TokenTransferMetadata> _parser = new pb::MessageParser<TokenTransferMetadata>(() => new TokenTransferMetadata());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pb::MessageParser<TokenTransferMetadata> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Ramp.RampCommonMessageReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public TokenTransferMetadata() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public TokenTransferMetadata(TokenTransferMetadata other) : this() {
      targetChainId_ = other.targetChainId_;
      tokenAddress_ = other.tokenAddress_;
      symbol_ = other.symbol_;
      amount_ = other.amount_;
      extraData_ = other.extraData_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public TokenTransferMetadata Clone() {
      return new TokenTransferMetadata(this);
    }

    /// <summary>Field number for the "target_chain_id" field.</summary>
    public const int TargetChainIdFieldNumber = 1;
    private long targetChainId_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public long TargetChainId {
      get { return targetChainId_; }
      set {
        targetChainId_ = value;
      }
    }

    /// <summary>Field number for the "token_address" field.</summary>
    public const int TokenAddressFieldNumber = 2;
    private string tokenAddress_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public string TokenAddress {
      get { return tokenAddress_; }
      set {
        tokenAddress_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "symbol" field.</summary>
    public const int SymbolFieldNumber = 3;
    private string symbol_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public string Symbol {
      get { return symbol_; }
      set {
        symbol_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "amount" field.</summary>
    public const int AmountFieldNumber = 4;
    private long amount_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public long Amount {
      get { return amount_; }
      set {
        amount_ = value;
      }
    }

    /// <summary>Field number for the "extra_data" field.</summary>
    public const int ExtraDataFieldNumber = 5;
    private pb::ByteString extraData_ = pb::ByteString.Empty;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public pb::ByteString ExtraData {
      get { return extraData_; }
      set {
        extraData_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override bool Equals(object other) {
      return Equals(other as TokenTransferMetadata);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool Equals(TokenTransferMetadata other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (TargetChainId != other.TargetChainId) return false;
      if (TokenAddress != other.TokenAddress) return false;
      if (Symbol != other.Symbol) return false;
      if (Amount != other.Amount) return false;
      if (ExtraData != other.ExtraData) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override int GetHashCode() {
      int hash = 1;
      if (TargetChainId != 0L) hash ^= TargetChainId.GetHashCode();
      if (TokenAddress.Length != 0) hash ^= TokenAddress.GetHashCode();
      if (Symbol.Length != 0) hash ^= Symbol.GetHashCode();
      if (Amount != 0L) hash ^= Amount.GetHashCode();
      if (ExtraData.Length != 0) hash ^= ExtraData.GetHashCode();
      if (_unknownFields != null) {
        hash ^= _unknownFields.GetHashCode();
      }
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void WriteTo(pb::CodedOutputStream output) {
      if (TargetChainId != 0L) {
        output.WriteRawTag(8);
        output.WriteInt64(TargetChainId);
      }
      if (TokenAddress.Length != 0) {
        output.WriteRawTag(18);
        output.WriteString(TokenAddress);
      }
      if (Symbol.Length != 0) {
        output.WriteRawTag(26);
        output.WriteString(Symbol);
      }
      if (Amount != 0L) {
        output.WriteRawTag(32);
        output.WriteInt64(Amount);
      }
      if (ExtraData.Length != 0) {
        output.WriteRawTag(42);
        output.WriteBytes(ExtraData);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public int CalculateSize() {
      int size = 0;
      if (TargetChainId != 0L) {
        size += 1 + pb::CodedOutputStream.ComputeInt64Size(TargetChainId);
      }
      if (TokenAddress.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(TokenAddress);
      }
      if (Symbol.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(Symbol);
      }
      if (Amount != 0L) {
        size += 1 + pb::CodedOutputStream.ComputeInt64Size(Amount);
      }
      if (ExtraData.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeBytesSize(ExtraData);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(TokenTransferMetadata other) {
      if (other == null) {
        return;
      }
      if (other.TargetChainId != 0L) {
        TargetChainId = other.TargetChainId;
      }
      if (other.TokenAddress.Length != 0) {
        TokenAddress = other.TokenAddress;
      }
      if (other.Symbol.Length != 0) {
        Symbol = other.Symbol;
      }
      if (other.Amount != 0L) {
        Amount = other.Amount;
      }
      if (other.ExtraData.Length != 0) {
        ExtraData = other.ExtraData;
      }
      _unknownFields = pb::UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(pb::CodedInputStream input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input);
            break;
          case 8: {
            TargetChainId = input.ReadInt64();
            break;
          }
          case 18: {
            TokenAddress = input.ReadString();
            break;
          }
          case 26: {
            Symbol = input.ReadString();
            break;
          }
          case 32: {
            Amount = input.ReadInt64();
            break;
          }
          case 42: {
            ExtraData = input.ReadBytes();
            break;
          }
        }
      }
    }

  }

  #endregion

}

#endregion Designer generated code
