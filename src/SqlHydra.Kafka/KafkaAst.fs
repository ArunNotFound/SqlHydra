namespace SqlHydra.Kafka

open System

/// Represents an abstract syntax tree for ksqlDB streaming queries
type KafkaAst =
    | Topic of name: string
    | Where of condition: string
    | Select of fields: string list
    | GroupBy of field: string
    | Window of windowType: string
    | EmitChanges

type StreamQuery = {
    Topic: string option
    Selects: string list
    Wheres: string list
    GroupByField: string option
    WindowType: string option
}

module StreamQuery =
    let empty = { Topic = None; Selects = []; Wheres = []; GroupByField = None; WindowType = None }

    let toKsql (q: StreamQuery) =
        let selectStr = if q.Selects.IsEmpty then "*" else q.Selects |> String.concat ", "
        
        let topicStr = 
            match q.Topic with
            | Some t -> t
            | None -> failwith "Topic is required in a kstream query"

        let whereStr = 
            if q.Wheres.IsEmpty then "" 
            else " WHERE " + (q.Wheres |> String.concat " AND ")

        let groupStr =
            match q.GroupByField with
            | Some g -> " GROUP BY " + g
            | None -> ""

        let windowStr =
            match q.WindowType with
            | Some w -> " WINDOW " + w
            | None -> ""

        sprintf "SELECT %s FROM %s%s%s%s EMIT CHANGES;" selectStr topicStr windowStr whereStr groupStr

type KStreamBuilder() =
    member _.Yield(_) = StreamQuery.empty

    [<CustomOperation("consume")>]
    member _.Consume(q: StreamQuery, topic: string) =
        { q with Topic = Some topic }

    [<CustomOperation("select")>]
    member _.Select(q: StreamQuery, field: string) =
        { q with Selects = q.Selects @ [field] }

    [<CustomOperation("where")>]
    member _.Where(q: StreamQuery, condition: string) =
        { q with Wheres = q.Wheres @ [condition] }

    [<CustomOperation("groupBy")>]
    member _.GroupBy(q: StreamQuery, field: string) =
        { q with GroupByField = Some field }

    [<CustomOperation("window")>]
    member _.Window(q: StreamQuery, windowType: string) =
        { q with WindowType = Some windowType }

    member _.Run(q: StreamQuery) =
        StreamQuery.toKsql q

[<AutoOpen>]
module Builder =
    let kstream = KStreamBuilder()
