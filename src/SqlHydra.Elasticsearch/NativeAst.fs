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

type RAggregation =
    | TermsAgg of field: string
    | DateHistogramAgg of field: string * interval: string
    | AvgAgg of field: string
    | SumAgg of field: string

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
    | RSort of field: string * direction: string
    | RSearchAfter of values: obj list
    | RAgg of name: string * agg: RAggregation
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

    [<CustomOperation("agg")>]
    member _.Agg(query: Raw, name: string, agg: RAggregation) =
        let node = RAgg(name, agg)
        match query with
        | RBool(must, should, mustNot, filter) -> RBool(node :: must, should, mustNot, filter)
        | _ -> RBool([node; query], [], [], [])

    [<CustomOperation("sortBy")>]
    member _.SortBy(query: Raw, field: string) =
        let node = RSort(field, "asc")
        match query with
        | RBool(must, should, mustNot, filter) -> RBool(node :: must, should, mustNot, filter)
        | _ -> RBool([node; query], [], [], [])

    [<CustomOperation("sortDescending")>]
    member _.SortDescending(query: Raw, field: string) =
        let node = RSort(field, "desc")
        match query with
        | RBool(must, should, mustNot, filter) -> RBool(node :: must, should, mustNot, filter)
        | _ -> RBool([node; query], [], [], [])

    [<CustomOperation("searchAfter")>]
    member _.SearchAfter(query: Raw, values: obj list) =
        let node = RSearchAfter(values)
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
            | RRaw(json, _) -> json
            | _ -> "" // Handled at root level
            
        let rec extractRootNodes (ast: Raw) =
            match ast with
            | RBool(must, _, _, _) -> must
            | _ -> [ast]

        let nodes = extractRootNodes query
        
        // Filter into query nodes and root-level modifiers
        let queryNodes = nodes |> List.filter (function | RAgg _ | RSort _ | RSearchAfter _ -> false | _ -> true)
        let aggNodes = nodes |> List.choose (function | RAgg(name, agg) -> Some(name, agg) | _ -> None)
        let sortNodes = nodes |> List.choose (function | RSort(f, d) -> Some(f, d) | _ -> None)
        let searchAfterNode = nodes |> List.tryPick (function | RSearchAfter(v) -> Some(v) | _ -> None)

        // 1. Build Query Block
        let queryJson = 
            if queryNodes.IsEmpty then """{"match_all": {}}"""
            else
                let mustJson = queryNodes |> List.map toJson |> String.concat ","
                sprintf """{"bool": {"must": [%s]}}""" mustJson

        // 2. Build Aggs Block
        let aggsJson =
            if aggNodes.IsEmpty then ""
            else
                let aggParts = 
                    aggNodes |> List.map (fun (name, agg) ->
                        let aggDef = 
                            match agg with
                            | TermsAgg(f) -> sprintf """{"terms": {"field": "%s"}}""" f
                            | DateHistogramAgg(f, i) -> sprintf """{"date_histogram": {"field": "%s", "calendar_interval": "%s"}}""" f i
                            | AvgAgg(f) -> sprintf """{"avg": {"field": "%s"}}""" f
                            | SumAgg(f) -> sprintf """{"sum": {"field": "%s"}}""" f
                        sprintf """"%s": %s""" name aggDef
                    ) |> String.concat ","
                sprintf """, "aggs": {%s}""" aggParts

        // 3. Build Sort Block
        let sortJson =
            if sortNodes.IsEmpty then ""
            else
                let sortParts = sortNodes |> List.map (fun (f, d) -> sprintf """{"%s": "%s"}""" f d) |> String.concat ","
                sprintf """, "sort": [%s]""" sortParts

        // 4. Build Search After Block
        let searchAfterJson =
            match searchAfterNode with
            | Some values -> 
                let valsStr = 
                    values 
                    |> List.map (fun v -> 
                        match v with
                        | :? string as s -> sprintf "\"%s\"" s
                        | _ -> sprintf "%O" v) 
                    |> String.concat ","
                sprintf """, "search_after": [%s]""" valsStr
            | None -> ""

        sprintf """{"query": %s%s%s%s}""" queryJson aggsJson sortJson searchAfterJson

[<AutoOpen>]
module Builder =
    let esquery = EsQueryBuilder()
