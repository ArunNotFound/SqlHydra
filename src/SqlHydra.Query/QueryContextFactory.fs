namespace SqlHydra.Query

open System.Data.Common

type QueryContextFactory =
    {
        ConnectionString: string
        OpenConnection: unit -> DbConnection
        OpenContext: unit -> QueryContext
    }
