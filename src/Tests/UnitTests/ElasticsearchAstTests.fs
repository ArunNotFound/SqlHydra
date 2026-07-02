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

[<Test>]
let ``Semantic KNN Search Translation`` () =
    let json =
        esquery {
            knn "title_vector" [0.1; 0.2; 0.3] 5 50
        }
    let expected = """{"query": {"bool": {"must": [{"knn": {"field": "title_vector", "query_vector": [0.1,0.2,0.3], "k": 5, "num_candidates": 50}}]}}}"""
    test <@ json = expected @>

[<Test>]
let ``Multi-Match Fuzzy Search Translation`` () =
    let json =
        esquery {
            multiMatch "inception" ["title"; "description"] "AUTO"
        }
    let expected = """{"query": {"bool": {"must": [{"multi_match": {"query": "inception", "fields": ["title","description"], "fuzziness": "AUTO"}}]}}}"""
    test <@ json = expected @>
