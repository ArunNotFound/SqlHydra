module SqlHydra.Elasticsearch.Tests.ElasticsearchAstTests

open NUnit.Framework
open Swensen.Unquote
open SqlHydra.Elasticsearch.Native

[<Test>]
let ``Simple Match Query Translation`` () =
    let json = 
        esquery {
            where (RMatch("Title", "Inception"))
        }
    
    let expected = """{"query": {"bool": {"must": [{"match": {"Title": "Inception"}}]}}}"""
    test <@ json = expected @>
    
[<Test>]
let ``Compound Boolean Query Translation`` () =
    let json = 
        esquery {
            where (RMatch("Title", "Batman"))
            where (RTerm("Year", 2008))
        }
    
    let expected = """{"query": {"bool": {"must": [{"term": {"Year": "2008"}},{"match": {"Title": "Batman"}}]}}}"""
    test <@ json = expected @>
    
[<Test>]
let ``Nested Object Query Translation`` () =
    let json =
        esquery {
            nested "actors" (RMatch("actors.name", "DiCaprio"))
        }
    let expected = """{"query": {"bool": {"must": [{"nested": {"path": "actors", "query": {"match": {"actors.name": "DiCaprio"}}}}]}}}"""
    test <@ json = expected @>

[<Test>]
let ``Has Child Query Translation`` () =
    let json =
        esquery {
            hasChild "review" (RTerm("rating", 5))
        }
    let expected = """{"query": {"bool": {"must": [{"has_child": {"type": "review", "query": {"term": {"rating": "5"}}}}]}}}"""
    test <@ json = expected @>

[<Test>]
let ``Has Parent Query Translation`` () =
    let json =
        esquery {
            hasParent "movie" (RMatch("genre", "Sci-Fi"))
        }
    let expected = """{"query": {"bool": {"must": [{"has_parent": {"parent_type": "movie", "query": {"match": {"genre": "Sci-Fi"}}}}]}}}"""
    test <@ json = expected @>
