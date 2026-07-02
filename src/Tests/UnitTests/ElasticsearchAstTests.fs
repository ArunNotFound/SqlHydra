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

[<Test>]
let ``Aggregation Date Histogram Translation`` () =
    let json =
        esquery {
            agg "sales_over_time" (DateHistogramAgg("date", "month"))
        }
    let expected = """{"query": {"match_all": {}}, "aggs": {"sales_over_time": {"date_histogram": {"field": "date", "calendar_interval": "month"}}}}"""
    test <@ json = expected @>

[<Test>]
let ``Sort and Search After Pagination Translation`` () =
    let json =
        esquery {
            where (RTerm("category", "electronics"))
            sortBy "price"
            searchAfter [ box 150.0; box "id_789" ]
        }
    let expected = """{"query": {"bool": {"must": [{"term": {"category": "electronics"}}]}}, "sort": [{"price": "asc"}], "search_after": [150,"id_789"]}"""
    test <@ json = expected @>

