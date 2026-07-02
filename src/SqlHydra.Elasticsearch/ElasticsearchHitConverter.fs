namespace SqlHydra.Elasticsearch

open System
open System.Text.Json
open System.Text.Json.Serialization
open System.Reflection

/// Base interface for Elasticsearch records that support dynamic schema evolution
type IElasticsearchRecord =
    abstract member AdditionalProperties: Map<string, obj> with get, set

/// Custom JSON Converter that unwraps the Elasticsearch `hits.hits[]._source` 
/// and maps dynamic properties into `AdditionalProperties`.
type ElasticsearchHitConverter<'T when 'T :> IElasticsearchRecord and 'T: not struct>() =
    inherit JsonConverter<'T>()

    override _.Read(reader: byref<Utf8JsonReader>, typeToConvert: Type, options: JsonSerializerOptions) : 'T =
        use doc = JsonDocument.ParseValue(&reader)
        let root = doc.RootElement
        
        // Unwrap `_source` if this is a hit object
        let sourceElement = 
            if root.TryGetProperty("_source") |> fst then
                root.GetProperty("_source")
            else
                root

        let instance = Activator.CreateInstance(typeToConvert) :?> 'T
        let properties = typeToConvert.GetProperties(BindingFlags.Public ||| BindingFlags.Instance)
        let propertyNames = properties |> Array.map (fun p -> p.Name.ToLowerInvariant()) |> Set.ofArray

        let mutable dynamicProps = Map.empty<string, obj>

        for prop in sourceElement.EnumerateObject() do
            let propName = prop.Name
            
            if propertyNames.Contains(propName.ToLowerInvariant()) && propName <> "AdditionalProperties" then
                let targetProp = properties |> Array.find (fun p -> p.Name.ToLowerInvariant() = propName.ToLowerInvariant())
                if targetProp.CanWrite then
                    let value = JsonSerializer.Deserialize(prop.Value.GetRawText(), targetProp.PropertyType, options)
                    targetProp.SetValue(instance, value)
            else
                // Dynamic mapping: Stuff unknown fields into AdditionalProperties
                let value = 
                    match prop.Value.ValueKind with
                    | JsonValueKind.String -> prop.Value.GetString() :> obj
                    | JsonValueKind.Number -> prop.Value.GetDouble() :> obj
                    | JsonValueKind.True -> true :> obj
                    | JsonValueKind.False -> false :> obj
                    | _ -> prop.Value.GetRawText() :> obj // Store complex dynamic types as raw JSON strings
                dynamicProps <- dynamicProps.Add(propName, value)

        instance.AdditionalProperties <- dynamicProps
        instance

    override _.Write(writer: Utf8JsonWriter, value: 'T, options: JsonSerializerOptions) =
        JsonSerializer.Serialize(writer, value, options)

type ElasticsearchHitConverterFactory() =
    inherit JsonConverterFactory()

    override _.CanConvert(typeToConvert: Type) =
        typeof<IElasticsearchRecord>.IsAssignableFrom(typeToConvert)

    override _.CreateConverter(typeToConvert: Type, options: JsonSerializerOptions) =
        let converterType = typedefof<ElasticsearchHitConverter<_>>.MakeGenericType([| typeToConvert |])
        Activator.CreateInstance(converterType) :?> JsonConverter
