/**
 * Autogenerated by Thrift Compiler (0.10.0)
 *
 * DO NOT EDIT UNLESS YOU ARE SURE THAT YOU KNOW WHAT YOU ARE DOING
 *  @generated
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Thrift;
using Thrift.Collections;
using System.Runtime.Serialization;
using Thrift.Protocol;
using Thrift.Transport;

namespace TheGraph.Thrift
{

  #if !SILVERLIGHT
  [Serializable]
  #endif
  public partial class edge : TBase
  {
    private int _v1;
    private int _v2;
    private double _weight;
    private bool _directed;
    private string _description;
    
        #region operadores
        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            var e2 = obj as edge;
            if (e2 == null)
                return false;

            if(this.Directed == e2.Directed) 
            {
                return (this.Directed && (this.V1 == e2.V1 && this.V2 == e2.V2)) ||
                       (!this.Directed && ((this.V1 == e2.V1 && this.V2 == e2.V2) || (this.V1 == e2.V1 && this.V2 == e2.V2)));
            }
            
            return false;
        }
        #endregion

    public int V1
    {
      get
      {
        return _v1;
      }
      set
      {
        __isset.v1 = true;
        this._v1 = value;
      }
    }

    public int V2
    {
      get
      {
        return _v2;
      }
      set
      {
        __isset.v2 = true;
        this._v2 = value;
      }
    }

    public double Weight
    {
      get
      {
        return _weight;
      }
      set
      {
        __isset.weight = true;
        this._weight = value;
      }
    }

    public bool Directed
    {
      get
      {
        return _directed;
      }
      set
      {
        __isset.directed = true;
        this._directed = value;
      }
    }

    public string Description
    {
      get
      {
        return _description;
      }
      set
      {
        __isset.description = true;
        this._description = value;
      }
    }


    public Isset __isset;
    #if !SILVERLIGHT
    [Serializable]
    #endif
    public struct Isset {
      public bool v1;
      public bool v2;
      public bool weight;
      public bool directed;
      public bool description;
    }

    public edge() {
    }

    public void Read (TProtocol iprot)
    {
      iprot.IncrementRecursionDepth();
      try
      {
        TField field;
        iprot.ReadStructBegin();
        while (true)
        {
          field = iprot.ReadFieldBegin();
          if (field.Type == TType.Stop) { 
            break;
          }
          switch (field.ID)
          {
            case 1:
              if (field.Type == TType.I32) {
                V1 = iprot.ReadI32();
              } else { 
                TProtocolUtil.Skip(iprot, field.Type);
              }
              break;
            case 2:
              if (field.Type == TType.I32) {
                V2 = iprot.ReadI32();
              } else { 
                TProtocolUtil.Skip(iprot, field.Type);
              }
              break;
            case 3:
              if (field.Type == TType.Double) {
                Weight = iprot.ReadDouble();
              } else { 
                TProtocolUtil.Skip(iprot, field.Type);
              }
              break;
            case 4:
              if (field.Type == TType.Bool) {
                Directed = iprot.ReadBool();
              } else { 
                TProtocolUtil.Skip(iprot, field.Type);
              }
              break;
            case 5:
              if (field.Type == TType.String) {
                Description = iprot.ReadString();
              } else { 
                TProtocolUtil.Skip(iprot, field.Type);
              }
              break;
            default: 
              TProtocolUtil.Skip(iprot, field.Type);
              break;
          }
          iprot.ReadFieldEnd();
        }
        iprot.ReadStructEnd();
      }
      finally
      {
        iprot.DecrementRecursionDepth();
      }
    }

    public void Write(TProtocol oprot) {
      oprot.IncrementRecursionDepth();
      try
      {
        TStruct struc = new TStruct("edge");
        oprot.WriteStructBegin(struc);
        TField field = new TField();
        if (__isset.v1) {
          field.Name = "v1";
          field.Type = TType.I32;
          field.ID = 1;
          oprot.WriteFieldBegin(field);
          oprot.WriteI32(V1);
          oprot.WriteFieldEnd();
        }
        if (__isset.v2) {
          field.Name = "v2";
          field.Type = TType.I32;
          field.ID = 2;
          oprot.WriteFieldBegin(field);
          oprot.WriteI32(V2);
          oprot.WriteFieldEnd();
        }
        if (__isset.weight) {
          field.Name = "weight";
          field.Type = TType.Double;
          field.ID = 3;
          oprot.WriteFieldBegin(field);
          oprot.WriteDouble(Weight);
          oprot.WriteFieldEnd();
        }
        if (__isset.directed) {
          field.Name = "directed";
          field.Type = TType.Bool;
          field.ID = 4;
          oprot.WriteFieldBegin(field);
          oprot.WriteBool(Directed);
          oprot.WriteFieldEnd();
        }
        if (Description != null && __isset.description) {
          field.Name = "description";
          field.Type = TType.String;
          field.ID = 5;
          oprot.WriteFieldBegin(field);
          oprot.WriteString(Description);
          oprot.WriteFieldEnd();
        }
        oprot.WriteFieldStop();
        oprot.WriteStructEnd();
      }
      finally
      {
        oprot.DecrementRecursionDepth();
      }
    }

    public override string ToString() {
      StringBuilder __sb = new StringBuilder("edge(");
      bool __first = true;
      if (__isset.v1) {
        if(!__first) { __sb.Append(", "); }
        __first = false;
        __sb.Append("V1: ");
        __sb.Append(V1);
      }
      if (__isset.v2) {
        if(!__first) { __sb.Append(", "); }
        __first = false;
        __sb.Append("V2: ");
        __sb.Append(V2);
      }
      if (__isset.weight) {
        if(!__first) { __sb.Append(", "); }
        __first = false;
        __sb.Append("Weight: ");
        __sb.Append(Weight);
      }
      if (__isset.directed) {
        if(!__first) { __sb.Append(", "); }
        __first = false;
        __sb.Append("Directed: ");
        __sb.Append(Directed);
      }
      if (Description != null && __isset.description) {
        if(!__first) { __sb.Append(", "); }
        __first = false;
        __sb.Append("Description: ");
        __sb.Append(Description);
      }
      __sb.Append(")");
      return __sb.ToString();
    }

  }

}
