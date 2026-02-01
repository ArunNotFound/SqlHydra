module SqlServer.``selectTask Tests``

open SqlHydra.Query
open Swensen.Unquote
open NUnit.Framework
open DB

#if NET8_0
open SqlServer.AdventureWorksNet8
#endif
#if NET9_0
open SqlServer.AdventureWorksNet9
#endif
#if NET10_0
open SqlServer.AdventureWorksNet10
#endif

[<Test>]
let ``selectTask - no select``() = task {
    let! results = 
        selectTask db {
            for p in Person.Person do
            take 10
        }
        
    gt0 results
}

[<Test>]
let ``selectTask - select p``() = task {
    let! results = 
        selectTask db {
            for p in Person.Person do
            take 10
            select p
        }
        
    gt0 results
}

[<Test>]
let ``selectTask - toArray``() = task {
    let! results = 
        selectTask db {
            for p in Person.Person do
            take 10
            toArray
        }
        
    gt0 results
}

[<Test>]
let ``selectTask - mapList column``() = task {
    let! results = 
        selectTask db {
            for p in Person.Person do
            take 10
            mapList p.FirstName
        }
        
    gt0 results
}

[<Test>]
let ``selectTask - select entity - mapSeq column``() = task {
    let! results = 
        selectTask db {
            for p in Person.Person do
            take 10
            select p
            mapSeq $"{p.FirstName} {p.LastName}"
        }
        
    gt0 results
}

[<Test>]
let ``selectTask - select columns into - mapList column``() = task {
    let! results = 
        selectTask db {
            for p in Person.Person do
            take 10
            select (p.FirstName, p.LastName) into (fname, lname)
            mapList $"{fname} {lname}"
        }
        
    gt0 results
}

[<Test>]
let ``selectTask - count``() = task {
    let! results = 
        selectTask db {
            for p in Person.Person do
            count
        }
        
    results >! 0
}

[<Test>]
let ``selectTask - tryHead - Selected``() = task {
    let! result = 
        selectTask db {
            for p in Person.Person do
            take 1
            tryHead
        }
        
    result |> Option.isSome =! true
}

[<Test>]
let ``selectTask - tryHead - Mapped``() = task {
    let! result = 
        selectTask db {
            for p in Person.Person do
            take 1
            mapSeq $"{p.FirstName} {p.LastName}"
            tryHead
        }
        
    result |> Option.isSome =! true
}


[<Test>]
let ``selectExpr - complex expression``() = task {
    let! results = 
        selectTask db {
            for p in Person.Person do
            take 10
            selectExpr (
                if p.FirstName = "John" 
                then $"{p.FirstName} {p.LastName} (VIP)"
                else $"{p.FirstName} <> John"
            )
        }
        
    results |> Seq.iter (printf "%s\n")
    gt0 results
}

[<Test>]
let ``selectExpr - leftJoin`` () = task {
    let! results = 
        selectTask db  {
            for o in Sales.SalesOrderHeader do
            leftJoin sr in Sales.SalesOrderHeaderSalesReason on (o.SalesOrderID = sr.Value.SalesOrderID)
            leftJoin r in Sales.SalesReason on (sr.Value.SalesReasonID = r.Value.SalesReasonID)
            where (isNotNullValue r.Value.Name)
            select (o, r |> Option.map _.ReasonType, r |> Option.map _.Name) into selected
            mapArray (
                let order, reason, name = selected
                $"Order: {order.SalesOrderID}, Reason: {reason}, Name: {name}\n"
            )
            take 10
        }

    results |> Array.iter (printf "%s")
    gt0 results
}

[<Test>]
let ``selectExpr - leftJoin 2`` () = task {
    let! results = 
        selectTask db  {
            for o in Sales.SalesOrderHeader do
            leftJoin sr in Sales.SalesOrderHeaderSalesReason on (o.SalesOrderID = sr.Value.SalesOrderID)
            leftJoin r in Sales.SalesReason on (sr.Value.SalesReasonID = r.Value.SalesReasonID)
            where (isNotNullValue r.Value.Name)
            selectExpr (
                match r with
                | Some reason -> $"Order: {o.SalesOrderID}, Reason: {reason.ReasonType}\n"
                | None -> "No Reason Given"                
            )
            take 10
        }

    results |> Seq.iter (printf "%s")
    gt0 results
}

open SqlHydra.Query.SqlServerExtensions
open type SqlFn

[<Test>]
let ``selectExpr - leftJoin - provenance`` () = task {

    // `UPPER(reason.ReasonType)` should be `UPPER(r.ReasonType)` when added to SQL SELECT to prove provenance is maintained.

    let! results = 
        selectTask db  {
            for o in Sales.SalesOrderHeader do
            leftJoin sr in Sales.SalesOrderHeaderSalesReason on (o.SalesOrderID = sr.Value.SalesOrderID)
            leftJoin r in Sales.SalesReason on (sr.Value.SalesReasonID = r.Value.SalesReasonID)
            where (isNotNullValue r.Value.Name)
            selectExpr (
                match r with
                | Some reason -> $"Order: {o.SalesOrderID}, Reason: {UPPER(reason.ReasonType)}\n"
                | None -> "No Reason Given"
                |> fun text -> text.Replace("Order:", "ORDER:")
            )
            take 10
        }

    results |> Seq.iter (printf "%s")
    gt0 results
}