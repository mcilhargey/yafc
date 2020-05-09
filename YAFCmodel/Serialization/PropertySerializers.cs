using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace YAFC.Model
{
    internal abstract class PropertySerializer<TOwner>
    {
        public readonly PropertyInfo property;
        public readonly JsonEncodedText propertyName;

        protected PropertySerializer(PropertyInfo property)
        {
            this.property = property;
            propertyName = JsonEncodedText.Encode(property.Name, JsonUtils.DefaultOptions.Encoder);
        }

        public override string ToString() => typeof(TOwner).Name + "." + property.Name;

        public abstract void SerializeToJson(TOwner owner, Utf8JsonWriter writer);
        public abstract void DeserializeFromJson(TOwner owner, ref Utf8JsonReader reader, List<ModelObject> allObjects);
        public abstract void SerializeToUndoBuilder(TOwner owner, UndoSnapshotBuilder builder);
        public abstract void DeserializeFromUndoBuilder(TOwner owner, UndoSnapshotReader reader);
        public virtual object DeserializeFromJson(ref Utf8JsonReader reader) => throw new NotSupportedException();
        public virtual bool CanBeNull() => false;
    }

    internal abstract class PropertySerializer<TOwner, TPropertyType> : PropertySerializer<TOwner>
    {
        protected readonly Action<TOwner, TPropertyType> setter;
        protected readonly Func<TOwner, TPropertyType> getter;
        
        protected PropertySerializer(PropertyInfo property) : base(property)
        {
            getter = property.GetGetMethod().CreateDelegate(typeof(Func<TOwner, TPropertyType>)) as Func<TOwner, TPropertyType>;
            setter = property.CanWrite ? property.GetSetMethod()?.CreateDelegate(typeof(Action<TOwner, TPropertyType>)) as Action<TOwner, TPropertyType> : null;
        }
    }

    internal class ValuePropertySerializer<TOwner, TPropertyType> : PropertySerializer<TOwner, TPropertyType>
    {
        private static readonly ValueSerializer<TPropertyType> ValueSerializer = ValueSerializer<TPropertyType>.Default;
        public ValuePropertySerializer(PropertyInfo property) : base(property) {}
        public override void SerializeToJson(TOwner owner, Utf8JsonWriter writer) => ValueSerializer.WriteToJson(writer, getter(owner));
        public override void DeserializeFromJson(TOwner owner, ref Utf8JsonReader reader, List<ModelObject> allObjects) => setter(owner, ValueSerializer.ReadFromJson(ref reader));
        public override void SerializeToUndoBuilder(TOwner owner, UndoSnapshotBuilder builder) => ValueSerializer.WriteToUndoSnapshot(builder, getter(owner));
        public override void DeserializeFromUndoBuilder(TOwner owner, UndoSnapshotReader reader) => setter(owner, ValueSerializer.ReadFromUndoSnapshot(reader));
        public override object DeserializeFromJson(ref Utf8JsonReader reader) => ValueSerializer.ReadFromJson(ref reader);
        public override bool CanBeNull() => ValueSerializer.CanBeNull();
    }

    // Serializes read-only sub-value with support of polymorphism
    internal class ReadOnlyReferenceSerializer<TOwner, TPropertyType> : PropertySerializer<TOwner, TPropertyType> where TOwner:ModelObject where TPropertyType : ModelObject
    {
        public ReadOnlyReferenceSerializer(PropertyInfo property) : base(property) {}

        public override void SerializeToJson(TOwner owner, Utf8JsonWriter writer)
        {
            var instance = getter(owner);
            if (instance == null)
                writer.WriteNullValue();
            else if (instance.GetType() == typeof(TPropertyType))
                SerializationMap<TPropertyType>.SerializeToJson(instance, writer);
            else
                SerializationMap.GetSerializationMap(instance.GetType()).SerializeToJson(instance, writer);
        }

        public override void DeserializeFromJson(TOwner owner, ref Utf8JsonReader reader, List<ModelObject> allObjects)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return;
            var instance = getter(owner);
            if (instance.GetType() == typeof(TPropertyType))
                SerializationMap<TPropertyType>.PopulateFromJson(getter(owner), ref reader, allObjects);
            else
                SerializationMap.GetSerializationMap(instance.GetType()).PopulateFromJson(instance, ref reader, allObjects);
        }
        public override void SerializeToUndoBuilder(TOwner owner, UndoSnapshotBuilder builder) {}
        public override void DeserializeFromUndoBuilder(TOwner owner, UndoSnapshotReader reader) {}
    }
    
    internal class ReadWriteReferenceSerializer<TOwner, TPropertyType> : ReadOnlyReferenceSerializer<TOwner, TPropertyType> where TOwner:ModelObject where TPropertyType : ModelObject
    {
        public ReadWriteReferenceSerializer(PropertyInfo property) : base(property) {}
        public override void DeserializeFromJson(TOwner owner, ref Utf8JsonReader reader, List<ModelObject> allObjects)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return;
            var instance = getter(owner);
            if (instance == null)
            {
                setter(owner, SerializationMap<TPropertyType>.DeserializeFromJson(owner, ref reader, allObjects));
                return;
            }
            base.DeserializeFromJson(owner, ref reader, allObjects);
        }

        public override void SerializeToUndoBuilder(TOwner owner, UndoSnapshotBuilder builder)
        {
            builder.WriteManagedReference(getter(owner));
        }

        public override void DeserializeFromUndoBuilder(TOwner owner, UndoSnapshotReader reader)
        {
            setter(owner, reader.ReadOwnedReference<TPropertyType>(owner));
        }
    }

    internal class ListOfValuesSerializer<TOwner, TListType> : PropertySerializer<TOwner, List<TListType>>
    {
        private static readonly ValueSerializer<TListType> ValueSerializer = ValueSerializer<TListType>.Default;
        public ListOfValuesSerializer(PropertyInfo property) : base(property) {}

        public override void SerializeToJson(TOwner owner, Utf8JsonWriter writer)
        {
            var list = getter(owner);
            writer.WriteStartArray();
            foreach (var elem in list)
                ValueSerializer.WriteToJson(writer, elem);
            writer.WriteEndArray();
        }

        public override void DeserializeFromJson(TOwner owner, ref Utf8JsonReader reader, List<ModelObject> allObjects)
        {
            var list = getter(owner);
            list.Clear();
            if (reader.ReadStartArray())
            {
                while (reader.TokenType != JsonTokenType.EndArray)
                {
                    list.Add(ValueSerializer.ReadFromJson(ref reader));
                    reader.Read();
                }
            }
        }

        public override void SerializeToUndoBuilder(TOwner owner, UndoSnapshotBuilder builder)
        {
            var list = getter(owner);
            builder.writer.Write(list.Count);
            foreach (var elem in list)
                ValueSerializer.WriteToUndoSnapshot(builder, elem);
        }

        public override void DeserializeFromUndoBuilder(TOwner owner, UndoSnapshotReader reader)
        {
            var list = getter(owner);
            list.Clear();
            var count = reader.reader.ReadInt32();
            if (list.Capacity < count)
                list.Capacity = count;
            for (var i = 0; i < count; i++)
                list.Add(ValueSerializer.ReadFromUndoSnapshot(reader));
        }
    }
    
    internal class ListOfReferencesSerializer<TOwner, TListType> : PropertySerializer<TOwner, List<TListType>> where TListType : ModelObject where TOwner:ModelObject
    {
        public ListOfReferencesSerializer(PropertyInfo property) : base(property) {}

        public override void SerializeToJson(TOwner owner, Utf8JsonWriter writer)
        {
            var list = getter(owner);
            writer.WriteStartArray();
            foreach (var elem in list)
                SerializationMap<TListType>.SerializeToJson(elem, writer);
            writer.WriteEndArray();
        }

        public override void DeserializeFromJson(TOwner owner, ref Utf8JsonReader reader, List<ModelObject> allObjects)
        {
            var list = getter(owner);
            list.Clear();
            if (reader.ReadStartArray())
            {
                while (reader.TokenType != JsonTokenType.EndArray)
                {
                    list.Add(SerializationMap<TListType>.DeserializeFromJson(owner, ref reader, allObjects));
                    reader.Read();
                }
            }
        }

        public override void SerializeToUndoBuilder(TOwner owner, UndoSnapshotBuilder builder)
        {
            var list = getter(owner);
            builder.writer.Write(list.Count);
            builder.WriteManagedReferences(list);
        }

        public override void DeserializeFromUndoBuilder(TOwner owner, UndoSnapshotReader reader)
        {
            var list = getter(owner);
            list.Clear();
            var count = reader.reader.ReadInt32();
            if (list.Capacity < count)
                list.Capacity = count;
            for (var i = 0; i < count; i++)
                list.Add(reader.ReadOwnedReference<TListType>(owner));
        }
    }
}