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
    | RTerm of field: string * value: obj
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

    member _.Run(query: Raw) =
        // Serialize AST to Elasticsearch Query DSL JSON String
        let rec toJson (ast: Raw) : string =
            match ast with
            | RMatch(field, value) -> 
                sprintf """{"match": {"%s": "%O"}}""" field value
            | RTerm(field, value) -> 
                sprintf """{"term": {"%s": "%O"}}""" field value
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
