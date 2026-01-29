namespace SqlHydra.Query

open System.Data.Common
open System.Threading.Tasks
open System.Runtime.CompilerServices

/// Factory for creating QueryContext instances and connections.
/// The QueryContextFactory implementations are created in the generated HydraBuilders modules.
type IQueryContextFactory =
    abstract member CreateConnection: unit -> DbConnection
    abstract member OpenContext: unit -> QueryContext
    abstract member OpenContextAsync: unit -> Task<QueryContext>

/// Convenience extension methods for IQueryContextFactory.
[<Extension>]
type IQueryContextFactoryExtensions =

    [<Extension>]
    static member OpenConnection(factory: IQueryContextFactory) =
        let conn = factory.CreateConnection()
        conn.Open()
        conn

    [<Extension>]
    static member OpenConnectionAsync(factory: IQueryContextFactory) =
        task {
            let conn = factory.CreateConnection()
            do! conn.OpenAsync()
            return conn
        }
