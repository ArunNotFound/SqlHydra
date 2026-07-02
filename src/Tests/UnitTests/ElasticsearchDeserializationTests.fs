module SqlHydra.Elasticsearch.Tests.ElasticsearchDeserializationTests

open System.Text.Json
open NUnit.Framework
open Swensen.Unquote
open SqlHydra.Elasticsearch

type MovieRecord = {
    Id: int
    Title: string
    mutable AdditionalProperties: Map<string, obj>
} with
    interface IElasticsearchRecord with
        member this.AdditionalProperties
            with get() = this.AdditionalProperties
            and set(v) = this.AdditionalProperties <- v

[<Test>]
let ``JsonConverter unwraps _source and extracts dynamic fields`` () =
    let hitJson = """
    {
        "_index": "movies",
        "_id": "1",
        "_score": 1.0,
        "_source": {
            "id": 1,
            "title": "Inception",
            "director": "Christopher Nolan",
            "box_office": 836.8
        }
    }
    """
    
    let options = JsonSerializerOptions()
    options.Converters.Add(ElasticsearchHitConverterFactory())
    options.PropertyNameCaseInsensitive <- true
    
    let movie = JsonSerializer.Deserialize<MovieRecord>(hitJson, options)
    
    // Statically typed fields map correctly
    test <@ movie.Id = 1 @>
    test <@ movie.Title = "Inception" @>
    
    // Unknown fields overflow into AdditionalProperties
    test <@ movie.AdditionalProperties.ContainsKey("director") @>
    test <@ (movie.AdditionalProperties.["director"] :?> string) = "Christopher Nolan" @>
    test <@ (movie.AdditionalProperties.["box_office"] :?> double) = 836.8 @>
