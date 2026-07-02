namespace SqlHydra.Elasticsearch.Native

type Lineage =
    | Exact of Set<string>
    | Declared of Set<string>
    | Opaque

module Lineage =
    let combine x y =
        match x, y with
        | Opaque, _ | _, Opaque -> Opaque
        | Declared a, Declared b -> Declared (Set.union a b)
        | Declared a, Exact b
        | Exact a, Declared b -> Declared (Set.union a b)
        | Exact a, Exact b -> Exact (Set.union a b)

type Raw =
    | RMatch of field: string * value: obj
    | RMatchPhrase of field: string * value: obj
    | RMultiMatch of query: string * fields: string list * fuzziness: string
    | RTerm of field: string * value: obj
    | RKnn of field: string * queryVector: float list * k: int * numCandidates: int
    | RBool of must: Raw list * should: Raw list * mustNot: Raw list * filter: Raw list
    | RNested of path: string * query: Raw
    | RHasChild of childType: string * query: Raw
    | RHasParent of parentType: string * query: Raw
    | RRaw of json: string * lineage: Lineage

type Expr<'row, 'value> = private Expr of Raw

module Expr =
    let toRaw (Expr r) = r

type EsQueryBuilder() =
    member _.Yield (()) = RBool([], [], [], [])
    
    [<CustomOperation("where")>]
    member _.Where(query: Raw, expr: Raw) =
        match query with
        | RBool(must, should, mustNot, filter) -> RBool(expr :: must, should, mustNot, filter)
        | _ -> RBool([expr; query], [], [], [])

    // Expose advanced operations
    [<CustomOperation("nested")>]
    member _.Nested(query: Raw, path: string, nestedQuery: Raw) =
        let node = RNested(path, nestedQuery)
        match query with
        | RBool(must, should, mustNot, filter) -> RBool(node :: must, should, mustNot, filter)
        | _ -> RBool([node; query], [], [], [])
        
    [<CustomOperation("hasChild")>]
    member _.HasChild(query: Raw, childType: string, childQuery: Raw) =
        let node = RHasChild(childType, childQuery)
        match query with
        | RBool(must, should, mustNot, filter) -> RBool(node :: must, should, mustNot, filter)
        | _ -> RBool([node; query], [], [], [])
        
    [<CustomOperation("hasParent")>]
    member _.HasParent(query: Raw, parentType: string, parentQuery: Raw) =
        let node = RHasParent(parentType, parentQuery)
        match query with
        | RBool(must, should, mustNot, filter) -> RBool(node :: must, should, mustNot, filter)
        | _ -> RBool([node; query], [], [], [])
        
    [<CustomOperation("matchPhrase")>]
    member _.MatchPhrase(query: Raw, field: string, value: obj) =
        let node = RMatchPhrase(field, value)
        match query with
        | RBool(must, should, mustNot, filter) -> RBool(node :: must, should, mustNot, filter)
        | _ -> RBool([node; query], [], [], [])

    [<CustomOperation("multiMatch")>]
    member _.MultiMatch(query: Raw, q: string, fields: string list, fuzziness: string) =
        let node = RMultiMatch(q, fields, fuzziness)
        match query with
        | RBool(must, should, mustNot, filter) -> RBool(node :: must, should, mustNot, filter)
        | _ -> RBool([node; query], [], [], [])
        
    [<CustomOperation("knn")>]
    member _.Knn(query: Raw, field: string, queryVector: float list, k: int, numCandidates: int) =
        let node = RKnn(field, queryVector, k, numCandidates)
        match query with
        | RBool(must, should, mustNot, filter) -> RBool(node :: must, should, mustNot, filter)
        | _ -> RBool([node; query], [], [], [])

    member _.Run(query: Raw) =
        // Serialize AST to Elasticsearch Query DSL JSON String
        let rec toJson (ast: Raw) : string =
            match ast with
            | RMatch(field, value) -> 
                sprintf """{"match": {"%s": "%O"}}""" field value
            | RMatchPhrase(field, value) -> 
                sprintf """{"match_phrase": {"%s": "%O"}}""" field value
            | RMultiMatch(q, fields, fuzziness) ->
                let fieldsJson = fields |> List.map (sprintf "\"%s\"") |> String.concat ","
                sprintf """{"multi_match": {"query": "%s", "fields": [%s], "fuzziness": "%s"}}""" q fieldsJson fuzziness
            | RTerm(field, value) -> 
                sprintf """{"term": {"%s": "%O"}}""" field value
            | RKnn(field, vector, k, numCandidates) ->
                let vectorStr = vector |> List.map string |> String.concat ","
                sprintf """{"knn": {"field": "%s", "query_vector": [%s], "k": %d, "num_candidates": %d}}""" field vectorStr k numCandidates
            | RNested(path, nestedQuery) ->
                sprintf """{"nested": {"path": "%s", "query": %s}}""" path (toJson nestedQuery)
            | RHasChild(childType, childQuery) ->
                sprintf """{"has_child": {"type": "%s", "query": %s}}""" childType (toJson childQuery)
            | RHasParent(parentType, parentQuery) ->
                sprintf """{"has_parent": {"parent_type": "%s", "query": %s}}""" parentType (toJson parentQuery)
            | RBool(must, _, _, _) -> 
                let mustJson = must |> List.map toJson |> String.concat ","
                sprintf """{"bool": {"must": [%s]}}""" mustJson
            | RRaw(json, _) -> json
            
        sprintf """{"query": %s}""" (toJson query)

[<AutoOpen>]
module Builder =
    let esquery = EsQueryBuilder()
