module SqlHydra.Tests.EtlImpedanceMismatchTests

open NUnit.Framework
open Swensen.Unquote
open SqlHydra.Elasticsearch
open SqlHydra.Elasticsearch.Native
open SqlHydra.Elasticsearch.IndexManager
open System.Text.Json

// 1. Simulating Upstream Relational Schema (Oracle)
type OracleOrder = { OrderId: int; CustomerName: string; OrderDate: System.DateTime }
type OracleOrderLine = { LineId: int; OrderId: int; ProductName: string; Quantity: int }
type OracleProduct = { ProductName: string; Category: string; Price: double }

// 2. Simulating DuckDB ETL Output (The Flattened vs Nested Payload)
// Model A: Flattened (Denormalized into 1 giant wide table)
type FlattenedEsModel = {
    OrderId: int
    CustomerName: string
    OrderDate: System.DateTime
    LineId: int
    ProductName: string
    Quantity: int
    Category: string
    Price: double
}

// Model B: Nested (Hierarchical arrays, great for read-heavy)
type NestedOrderLine = { LineId: int; ProductName: string; Quantity: int; Category: string; Price: double }
type NestedOrderModel = {
    OrderId: int
    CustomerName: string
    OrderDate: System.DateTime
    OrderLines: NestedOrderLine list
}

// Model C: Parent/Child (Strictly joined in ES, great for update-heavy)
type ParentOrderModel = {
    OrderId: int
    CustomerName: string
    OrderDate: System.DateTime
}

type ChildLineModel = {
    LineId: int
    ParentOrderId: int
    ProductName: string
    Quantity: int
    Category: string
    Price: double
}

[<Test>]
let ``Litmus Test: ESQuery correctly targets Flattened Denormalized Model`` () =
    let query =
        esquery {
            where (RTerm("CustomerName", "Alice"))
            where (RMatch("ProductName", "Widget"))
        }
    let expected = """{"query": {"bool": {"must": [{"match": {"ProductName": "Widget"}},{"term": {"CustomerName": "Alice"}}]}}}"""
    test <@ query = expected @>

[<Test>]
let ``Litmus Test: ESQuery correctly targets Nested Hierarchical Model`` () =
    // In the nested model, OrderLines is an array of objects.
    let query =
        esquery {
            where (RTerm("CustomerName", "Alice"))
            nested "OrderLines" (RMatch("OrderLines.ProductName", "Widget"))
        }
    let expected = """{"query": {"bool": {"must": [{"nested": {"path": "OrderLines", "query": {"match": {"OrderLines.ProductName": "Widget"}}}},{"term": {"CustomerName": "Alice"}}]}}}"""
    test <@ query = expected @>

[<Test>]
let ``Litmus Test: ESQuery correctly targets Parent/Child Join Model`` () =
    let query =
        esquery {
            where (RTerm("CustomerName", "Alice"))
            hasChild "orderLine" (RMatch("ProductName", "Widget"))
        }
    let expected = """{"query": {"bool": {"must": [{"has_child": {"type": "orderLine", "query": {"match": {"ProductName": "Widget"}}}},{"term": {"CustomerName": "Alice"}}]}}}"""
    test <@ query = expected @>
