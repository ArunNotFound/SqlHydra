module internal SqlHydra.Query.LinqExpressionVisitors

open System
open System.Linq.Expressions
open System.Reflection
open SqlKata
open FastExpressionCompiler

let notImpl() = raise (NotImplementedException())
let notImplMsg msg = raise (NotImplementedException msg)

[<AutoOpen>]
module VisitorPatterns =

    let (|Lambda|_|) (exp: Expression) =
        match exp.NodeType with
        | ExpressionType.Lambda -> Some (exp :?> LambdaExpression)
        | _ -> None

    let (|Unary|_|) (exp: Expression) =
        match exp.NodeType with
        | ExpressionType.ArrayLength
        | ExpressionType.Convert
        | ExpressionType.ConvertChecked
        | ExpressionType.Negate
        | ExpressionType.UnaryPlus
        | ExpressionType.NegateChecked
        | ExpressionType.Not
        | ExpressionType.Quote
        | ExpressionType.TypeAs -> Some (exp :?> UnaryExpression)
        | _ -> None

    let (|Binary|_|) (exp: Expression) =
        match exp.NodeType with
        | ExpressionType.Add
        | ExpressionType.AddChecked
        | ExpressionType.And
        | ExpressionType.AndAlso
        | ExpressionType.ArrayIndex
        | ExpressionType.Coalesce
        | ExpressionType.Divide
        | ExpressionType.Equal
        | ExpressionType.ExclusiveOr
        | ExpressionType.GreaterThan
        | ExpressionType.GreaterThanOrEqual
        | ExpressionType.LeftShift
        | ExpressionType.LessThan
        | ExpressionType.LessThanOrEqual
        | ExpressionType.Modulo
        | ExpressionType.Multiply
        | ExpressionType.MultiplyChecked
        | ExpressionType.NotEqual
        | ExpressionType.Or
        | ExpressionType.OrElse
        | ExpressionType.Power
        | ExpressionType.RightShift
        | ExpressionType.Subtract
        | ExpressionType.SubtractChecked -> Some (exp :?> BinaryExpression)
        | _ -> None

    let (|MethodCall|_|) (exp: Expression) =
        match exp.NodeType with
        | ExpressionType.Call -> Some (exp :?> MethodCallExpression)    
        | _ -> None
    let (|New|_|) (exp: Expression) =
        match exp.NodeType with
        | ExpressionType.New -> Some (exp :?> NewExpression)
        | _ -> None

    let (|Constant|_|) (exp: Expression) =
        match exp.NodeType with
        | ExpressionType.Constant -> Some (exp :?> ConstantExpression)
        | _ -> None
    
    let (|ImplConvertConstant|_|) (exp: Expression) =
        match exp.NodeType with
        | ExpressionType.Convert ->
            // Handles implicit conversion. Ex: upcasting int to an int64
            let unary = exp :?> UnaryExpression
            match unary.Operand with
            | Constant c when unary.Type.IsPrimitive -> Some c
            | _ -> None
            //Some (unary.Operand, unary.Type)
        | ExpressionType.Call -> 
            // Handles implicit conversion. Ex: casting an int to a decimal
            let mc = exp :?> MethodCallExpression
            match mc.Method.Name, mc.Arguments |> Seq.toList with
            | "op_Implicit", [ Constant c ] -> Some c
            | _ -> None
        | _ -> None
    
    let (|ArrayInit|_|) (exp: Expression) =
        match exp.NodeType with
        | ExpressionType.NewArrayInit -> 
            let arrayExp = exp :?> NewArrayExpression
            Some (arrayExp.Expressions |> Seq.map (function | Constant c -> c.Value | _ -> notImplMsg "Unable to unwrap array value."))
        | _ -> None

    let rec unwrapListExpr (lstValues: obj list, lstExp: MethodCallExpression) =
        if lstExp.Arguments.Count > 0 then
            match lstExp.Arguments.[0] with
            | Constant c -> unwrapListExpr (lstValues @ [c.Value], (lstExp.Arguments.[1] :?> MethodCallExpression))
            | _ -> notImpl()
        else 
            lstValues    

    let (|ListInit|_|) (exp: Expression) = 
        match exp with
        | MethodCall c when c.Method.Name = "Cons" ->
            let values = unwrapListExpr ([], c)
            Some values
        | _ -> None

    let (|Member|_|) (exp: Expression) =
        match exp.NodeType with
        | ExpressionType.MemberAccess -> Some (exp :?> MemberExpression)
        | _ -> None

    let (|BoolMember|_|) (exp: Expression) = 
        match exp with
        | Member m when m.Type = typeof<bool> -> Some m
        | _ -> None

    let (|BoolConstant|_|) (exp: Expression) = 
        match exp with
        | Constant c when c.Type = typeof<bool> -> Some (c.Value :?> bool)
        | _ -> None

    let (|Parameter|_|) (exp: Expression) =
        match exp.NodeType with
        | ExpressionType.Parameter -> Some (exp :?> ParameterExpression)
        | _ -> None

[<AutoOpen>]
module SqlPatterns = 

    let (|Not|_|) (exp: Expression) = 
        match exp.NodeType with
        | ExpressionType.Not -> Some ((exp :?> UnaryExpression).Operand)
        | _ -> None

    let (|BinaryAnd|_|) (exp: Expression) =
        match exp.NodeType with
        | ExpressionType.And
        | ExpressionType.AndAlso -> Some (exp :?> BinaryExpression)
        | _ -> None

    let (|BinaryOr|_|) (exp: Expression) =
        match exp.NodeType with
        | ExpressionType.Or
        | ExpressionType.OrElse -> Some (exp :?> BinaryExpression)
        | _ -> None

    let (|BinaryCompare|_|) (exp: Expression) =
        match exp.NodeType with
        | ExpressionType.Equal
        | ExpressionType.NotEqual
        | ExpressionType.GreaterThan
        | ExpressionType.GreaterThanOrEqual
        | ExpressionType.LessThan
        | ExpressionType.LessThanOrEqual -> Some (exp :?> BinaryExpression)
        | _ -> None

    let (|Call|_|) (exp: Expression) =
        match exp.NodeType with
        | ExpressionType.Call -> Some (exp :?> MethodCallExpression)
        | _ -> None

    let isOptionType (t: Type) = 
        t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Option<_>>

    let isNullableType (t: Type) = 
        t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Nullable<_>>

    let isOptionOrNullableType (t: Type) = 
        t.IsGenericType && (
            let genericTypeDef = t.GetGenericTypeDefinition()
            genericTypeDef = typedefof<Option<_>> || 
            genericTypeDef = typedefof<Nullable<_>>
        )

    let tryGetMember(x: Expression) = 
        match x with
        | Member m when m.Expression = null -> 
            None
        | Member m when m.Expression.NodeType = ExpressionType.Parameter || m.Expression.NodeType = ExpressionType.MemberAccess -> 
            Some m
        | MethodCall opt when opt.Type |> isOptionType ->        
            if opt.Arguments.Count > 0 then
                // Option.Some
                match opt.Arguments.[0] with
                | Member m -> Some m
                | _ -> None
            else None
        | MethodCall nul when nul.Type |> isNullableType -> 
            if nul.Arguments.Count > 0 then
                // Nullable.Value
                match nul.Arguments.[0] with
                | Member m -> Some m
                | _ -> None
            else None
        | Unary u when u.Operand.NodeType = ExpressionType.MemberAccess -> 
            Some (u.Operand :?> MemberExpression)
        | _ -> 
            None
                
    // Extract constant value from nested object/properties
    let rec unwrapMember (m: MemberExpression) =
        match m.Expression with
        | Constant c -> Some c.Value
        | Member m -> unwrapMember m
        | _ -> None

    let compileAndEvaluateExpression (exp: Expression) = 
        try
            let lambda = Expression.Lambda(exp)
            let compiled = lambda.CompileFast()
            compiled.DynamicInvoke()
        with ex ->  
            notImplMsg $"Unable to evaluate query parameter expression:\n{exp}"

    /// Handles extended properties on Nullable and Option types.
    [<RequireQualifiedAccess>]
    type ExtProperty = 
        | IsSome
        | IsNone
        | HasValue
        | Value
        | NA

    /// A property member with extended property info for Nullable and Option types.
    let (|Property|_|) (exp: Expression) =
        match exp with
        | Member m when 
            m.Member.DeclaringType <> null && 
            m.Member.DeclaringType |> isOptionOrNullableType && 
            (m.Member.Name = "Value" || m.Member.Name = "HasValue" || m.Member.Name = "IsSome" || m.Member.Name = "IsNone") -> 

            let ext = 
                match m.Member.Name with
                | "Value" -> ExtProperty.Value
                | "IsSome" -> ExtProperty.IsSome
                | "IsNone" -> ExtProperty.IsNone
                | "HasValue" -> ExtProperty.HasValue
                | _ -> ExtProperty.NA

            tryGetMember m.Expression
            |> Option.map (fun pm -> pm, ext)
        | _ -> 
            tryGetMember exp
            |> Option.map (fun pm -> pm, ExtProperty.NA)

    /// A property/column in a record/table mapped to this query via a `for` or `join` clause.
    let (|MappedColumn|_|) (tables: TableMapping seq) (exp: Expression) = 
        match exp with
        | Property (p, ext) when tables |> Seq.exists (fun tbl -> tbl.IsInTable p) ->
            Some (p, ext)
        | _ -> 
            None

    /// A constant value or an expression that can be evaluated to a constant value.
    let (|Value|_|) (exp: Expression) =
        match exp with
        | Constant c -> Some c.Value
        // Do not try to evaluate QueryFunctions like `isIn`, `isNotIn`, etc.
        | Call c when c.Method.Module.Name <> "SqlHydra.Query.dll" -> 
            compileAndEvaluateExpression exp |> Some
        | _ -> None

    let (|AggregateColumn|_|) (exp: Expression) =
        match exp with
        | MethodCall m when List.contains m.Method.Name [ nameof minBy; nameof maxBy; nameof sumBy; nameof avgBy; nameof countBy; nameof avgByAs ] ->
            let aggType = m.Method.Name.Replace("By", "").Replace("As", "").ToUpper()
            match m.Arguments.[0] with
            | Property p -> Some (aggType, p)
            | _ -> notImplMsg "Invalid argument to aggregate function."
        | _ -> None

let getComparison (expType: ExpressionType) =
    match expType with
    | ExpressionType.Equal -> "="
    | ExpressionType.NotEqual -> "<>"
    | ExpressionType.GreaterThan -> ">"
    | ExpressionType.GreaterThanOrEqual -> ">="
    | ExpressionType.LessThan -> "<"
    | ExpressionType.LessThanOrEqual -> "<="
    | _ -> notImplMsg "Unsupported comparison type"

let reverseComparison (expType: ExpressionType) =
    match expType with
    | ExpressionType.GreaterThan -> ExpressionType.LessThan
    | ExpressionType.GreaterThanOrEqual -> ExpressionType.LessThanOrEqual
    | ExpressionType.LessThan -> ExpressionType.GreaterThan
    | ExpressionType.LessThanOrEqual -> ExpressionType.GreaterThanOrEqual
    | _ -> expType


let getReverseComparison = getComparison << reverseComparison
    
let visitAlias (exp: Expression) =
    let rec visit (exp: Expression) =
        match exp with
        | Member m -> visit m.Expression
        | Parameter p -> p.Name
        | _ -> notImpl()
    visit exp

/// Converts a SQL function MethodCall expression to a SQL fragment string.
/// Example: LEN(p.FirstName) -> "LEN({p}.{FirstName})"
let rec visitSqlFn (qualifyColumn: string -> MemberInfo -> string) (exp: Expression) : string =
    match exp with
    | MethodCall m ->
        let fnName = m.Method.Name
        let args =
            m.Arguments
            |> Seq.map (fun arg ->
                match arg with
                | Member mem ->
                    let alias = visitAlias mem.Expression
                    qualifyColumn alias mem.Member
                | Constant c when c.Value = null ->
                    "NULL"
                | Constant c when c.Type = typeof<string> ->
                    $"'{c.Value}'"
                | Constant c ->
                    sprintf "%O" c.Value
                | MethodCall _ as nested ->
                    // Handle nested function calls
                    visitSqlFn qualifyColumn nested
                | _ ->
                    notImplMsg $"Unsupported argument type in SQL function: {arg.NodeType}"
            )
            |> String.concat ", "
        $"{fnName}({args})"
    | _ ->
        notImplMsg $"Expected a method call expression but got: {exp.NodeType}"


let visitWhere<'T> (tables: TableMapping seq) (filter: Expression<Func<'T, bool>>) (qualifyColumn: string -> MemberInfo -> string) =
    /// A column/property on a mapped table/record.
    let (|Column|_|) (exp: Expression) = 
        match exp with
        | MappedColumn tables (p, ext) -> Some (p, ext)
        | _ -> None

    let rec visit (exp: Expression) (query: Query) : Query =
        match exp with
        | Lambda x -> visit x.Body query
        | MethodCall m when m.Method.Name = "Invoke" ->
            // Handle tuples
            visit m.Object (Query())
        | MethodCall m when List.contains m.Method.Name [ nameof isIn; nameof isNotIn; nameof op_BarEqualsBar; nameof op_BarLessGreaterBar ] ->
            let filter : (string * seq<obj>) -> Query = 
                match m.Method.Name with
                | nameof isIn | nameof op_BarEqualsBar -> query.WhereIn
                | _ -> query.WhereNotIn

            match m.Arguments[0], m.Arguments[1] with
            // Column is IN / NOT IN a subquery of values
            | Column (p, _), MethodCall subqueryExpr when subqueryExpr.Method.Name = nameof subqueryMany ->
                let subqueryConst = match subqueryExpr.Arguments[0] with | Constant c -> c | _ -> notImpl()
                let alias = visitAlias p.Expression
                let fqCol = qualifyColumn alias p.Member
                let selectSubquery = subqueryConst.Value :?> SelectQuery
                match m.Method.Name with
                | nameof isIn | nameof op_BarEqualsBar -> query.WhereIn(fqCol, selectSubquery.ToKataQuery())
                | _ -> query.WhereNotIn(fqCol, selectSubquery.ToKataQuery())
            // Column is IN / NOT IN a list of values
            | Column (p, _), ListInit values ->
                let queryParameters = 
                    values 
                    |> Seq.map (KataUtils.getQueryParameterForValue p.Member)
                    |> Seq.toArray
                let alias = visitAlias p.Expression
                let fqCol = qualifyColumn alias p.Member
                filter(fqCol, queryParameters)
            // Column is IN / NOT IN an array of values
            | Column (p, _), ArrayInit values -> 
                let queryParameters = 
                    values 
                    |> Seq.map (KataUtils.getQueryParameterForValue p.Member)
                    |> Seq.toArray
                let alias = visitAlias p.Expression
                let fqCol = qualifyColumn alias p.Member
                filter(fqCol, queryParameters)
            // Column is IN / NOT IN an IEnumerable of values
            | Column (p, _), Value value -> 
                let queryParameters = 
                    (value :?> System.Collections.IEnumerable) 
                    |> Seq.cast<obj> 
                    |> Seq.map (KataUtils.getQueryParameterForValue p.Member)
                    |> Seq.toArray
                let alias = visitAlias p.Expression
                let fqCol = qualifyColumn alias p.Member
                filter(fqCol, queryParameters)
            // Column is IN / NOT IN a sequence expression of values
            | Column p, MethodCall c when c.Method.Name = "CreateSequence" ->
                notImplMsg "Unable to unwrap sequence expression. Please use a list or array instead."
            | _ -> notImpl()

        // like / notLike fns
        | MethodCall m when List.contains m.Method.Name [ nameof like; nameof notLike; nameof op_EqualsPercent; nameof op_LessGreaterPercent ] ->
            match m.Arguments.[0], m.Arguments.[1] with
            | Column (p, _), Value value -> 
                let pattern = string value
                let alias = visitAlias p.Expression
                let fqCol = qualifyColumn alias p.Member
                match m.Method.Name with
                | nameof like | nameof op_EqualsPercent -> query.WhereLike(fqCol, pattern, false)
                | _ -> query.WhereNotLike(fqCol, pattern, false)
            | _ -> notImpl()

        // isNull / isNotNull
        | MethodCall m when List.contains m.Method.Name [ nameof isNullValue; "IsNull"; nameof isNotNullValue ] ->
            match m.Arguments.[0] with
            | Column (p, _) -> 
                let alias = visitAlias p.Expression
                let fqCol = qualifyColumn alias p.Member
                if m.Method.Name = nameof isNullValue || m.Method.Name = "IsNull" // CompiledName for `isNull` = `IsNull`
                then query.WhereNull(fqCol)
                else query.WhereNotNull(fqCol)
            | _ -> notImpl()

        // areEqual / notEqual
        | MethodCall m when List.contains m.Method.Name [ nameof areEqual; nameof notEqual ] ->
            match m.Arguments.[0], m.Arguments.[1] with
            | Column (p1, _), Column (p2, _) -> 
                let alias1 = visitAlias p1.Expression
                let fqCol1 = qualifyColumn alias1 p1.Member
                let alias2 = visitAlias p2.Expression
                let fqCol2 = qualifyColumn alias2 p2.Member
                let comparison = if m.Method.Name = nameof areEqual then "=" else "<>"
                query.WhereColumns(fqCol1, comparison, fqCol2)
            | Column (p, _), Value value | Value value, Column (p, _) ->
                let alias1 = visitAlias p.Expression
                let fqCol1 = qualifyColumn alias1 p.Member
                let queryParameter = KataUtils.getQueryParameterForValue p.Member value
                let comparison = if m.Method.Name = nameof areEqual then "=" else "<>"
                query.Where(fqCol1, comparison, queryParameter)
            | Column (p, _), Value null | Value null, Column (p, _) ->
                let alias = visitAlias p.Expression
                let fqCol = qualifyColumn alias p.Member
                if m.Method.Name = nameof areEqual
                then query.WhereNull(fqCol) 
                else query.WhereNotNull(fqCol)
            | _ -> notImpl()
        
        // Nullable / Option .HasValue / .IsSome `where user.HasValue`; `where user.IsSome`
        | BoolMember (Column (p, ext)) when 
            p.Type |> isOptionOrNullableType 
            && (ext = ExtProperty.HasValue || ext = ExtProperty.IsSome) ->

            let alias = visitAlias p.Expression
            let m = tryGetMember p
            let fqCol = qualifyColumn alias m.Value.Member
            query.WhereNotNull(fqCol)

        // Negated Nullable / Option .HasValue/ .IsSome `where (not user.HasValue)`; `where (not user.IsSome)`
        | Not (BoolMember (Column (p, ext))) when 
            p.Type |> isOptionOrNullableType 
            && (ext = ExtProperty.HasValue || ext = ExtProperty.IsSome) ->

            let alias = visitAlias p.Expression
            let m = tryGetMember p
            let fqCol = qualifyColumn alias m.Value.Member
            query.WhereNull(fqCol)

        // Option.IsNone `where user.IsNone`
        | BoolMember (Column (p, ext)) when 
            p.Type |> isOptionType 
            && ext = ExtProperty.IsNone ->

            let alias = visitAlias p.Expression
            let m = tryGetMember p
            let fqCol = qualifyColumn alias m.Value.Member
            query.WhereNull(fqCol)

        // Negated Option.IsNone `where (not user.IsNone)`
        | Not (BoolMember (Column (p, ext))) when 
            p.Type |> isOptionType 
            && ext = ExtProperty.IsNone ->

            let alias = visitAlias p.Expression
            let m = tryGetMember p
            let fqCol = qualifyColumn alias m.Value.Member
            query.WhereNotNull(fqCol)

        // Bool column `where user.IsEnabled`; `where (user.IsEnabled.Value)`
        | BoolMember (Column (m, _)) ->
            let alias = visitAlias m.Expression
            let fqCol = qualifyColumn alias m.Member
            query.Where(fqCol, "=", true)

        | Not (BoolMember (Column (m, _))) -> // `where (not user.IsEnabled)`; `where (not user.IsEnabled.Value); NOTE: This must exist before `Not` handler.
            let alias = visitAlias m.Expression
            let fqCol = qualifyColumn alias m.Member
            query.Where(fqCol, "=", false)
        | Not operand ->
            let operand = visit operand (Query())
            query.WhereNot(fun q -> operand)
        | BinaryAnd x ->
            match x.Left with
            | Value enabled -> 
                if enabled :?> bool
                then visit x.Right (Query())
                else query // short-circuit: if left is false, right is not evaluated
            | _ -> 
                let lt = visit x.Left (Query())
                let rt = visit x.Right (Query())
                query.Where(fun q -> lt).Where(fun q -> rt)
        | BinaryOr x -> 
            match x.Left with
            // Allow user to enable or disable right side where clause
            | Value enabled  -> 
                if enabled :?> bool
                then visit x.Right (Query())
                else query // short-circuit: if left is false, right is not evaluated
            | _ -> 
                let lt = visit x.Left (Query())
                let rt = visit x.Right (Query())
                query.OrWhere(fun q -> lt).OrWhere(fun q -> rt)
        | BinaryCompare x ->
            match x.Left, x.Right with
            
            // Handle property to subquery comparisons
            | Column (p1, _), MethodCall subqueryExpr when subqueryExpr.Method.Name = nameof subqueryOne ->
                let comparison = getComparison exp.NodeType
                let subqueryConst = match subqueryExpr.Arguments.[0] with | Constant c -> c | _ -> notImpl()
                let selectSubquery = subqueryConst.Value :?> SelectQuery
                let alias = visitAlias p1.Expression
                let fqCol = qualifyColumn alias p1.Member
                query.Where(fqCol, comparison, selectSubquery.ToKataQuery())
            
            // Handle col to col comparisons
            | Column (p1, _), Column (p2, _) ->
                let lt = 
                    let alias = visitAlias p1.Expression
                    qualifyColumn alias p1.Member
                let comparison = getComparison exp.NodeType
                let rt = 
                    let alias = visitAlias p2.Expression
                    qualifyColumn alias p2.Member
                query.WhereColumns(lt, comparison, rt) 

            // Column = null comparisons
            | Column (p, _), Constant null | Constant null, Column (p, _) when exp.NodeType = ExpressionType.Equal ->
                let alias = visitAlias p.Expression
                let fqCol = qualifyColumn alias p.Member
                query.WhereNull(fqCol)
            
            // Column <> null comparisons
            | Column (p, _), Constant null | Constant null, Column (p, _) when exp.NodeType = ExpressionType.NotEqual ->
                let alias = visitAlias p.Expression
                let fqCol = qualifyColumn alias p.Member
                query.WhereNotNull(fqCol)
            
            // Option.IsSome / Nullable.HasValue null check
            | Column (p, ext), BoolConstant value | BoolConstant value, Column (p, ext) when 
                p.Type |> isOptionOrNullableType 
                && (ext = ExtProperty.HasValue || ext = ExtProperty.IsSome) 
                && exp.NodeType = ExpressionType.Equal ->

                let alias = visitAlias p.Expression
                let m = tryGetMember p
                let fqCol = qualifyColumn alias m.Value.Member
                match value with
                | true -> query.WhereNotNull(fqCol)
                | false -> query.WhereNull(fqCol)     
                
            // Option.IsSome/ Nullable.HasValue null check
            | Column (p, ext), BoolConstant value | BoolConstant value, Column (p, ext) when 
                p.Type |> isOptionOrNullableType 
                && (ext = ExtProperty.HasValue || ext = ExtProperty.IsSome) 
                && exp.NodeType = ExpressionType.NotEqual ->

                let alias = visitAlias p.Expression
                let fqCol = qualifyColumn alias p.Member
                match value with
                | true -> query.WhereNull(fqCol)
                | false -> query.WhereNotNull(fqCol)

            // Nullable.Value comparisons
            | Column (p, ext), Value value | Value value, Column (p, ext) when 
                p.Type |> isOptionOrNullableType 
                && ext = ExtProperty.Value ->

                let comparison = getComparison exp.NodeType
                let queryParameter = KataUtils.getQueryParameterForValue p.Member value
                let alias = visitAlias p.Expression
                let m = tryGetMember p
                let fqCol = qualifyColumn alias m.Value.Member
                query.Where(fqCol, comparison, queryParameter)

            | Column (p, _), _ -> 
                let value = x.Right |> compileAndEvaluateExpression
                let comparison = getComparison(exp.NodeType)
                let alias = visitAlias p.Expression
                let fqCol = qualifyColumn alias p.Member
                match value with
                | null when comparison = "=" -> 
                    query.WhereNull(fqCol)
                | null when comparison = "<>" -> 
                    query.WhereNotNull(fqCol)
                | _ -> 
                    let queryParameter = KataUtils.getQueryParameterForValue p.Member value
                    query.Where(fqCol, comparison, queryParameter)

            | _, Column (p, _) ->
                let value = x.Left |> compileAndEvaluateExpression
                let comparison = getReverseComparison(exp.NodeType)
                let alias = visitAlias p.Expression
                let fqCol = qualifyColumn alias p.Member
                match value with
                | null when comparison = "=" ->
                    query.WhereNull(fqCol)
                | null when comparison = "<>" ->
                    query.WhereNotNull(fqCol)
                | _ ->
                    let queryParameter = KataUtils.getQueryParameterForValue p.Member value
                    query.Where(fqCol, comparison, queryParameter)

            // SQL function compared to value: LEN(p.Name) > 5
            | MethodCall m, Value value ->
                let sqlFragment = visitSqlFn qualifyColumn (m :> Expression)
                let comparison = getComparison exp.NodeType
                query.WhereRaw($"{sqlFragment} {comparison} ?", [| value |])

            // Value compared to SQL function: 5 < LEN(p.Name)
            | Value value, MethodCall m ->
                let sqlFragment = visitSqlFn qualifyColumn (m :> Expression)
                let comparison = getReverseComparison exp.NodeType
                query.WhereRaw($"{sqlFragment} {comparison} ?", [| value |])

            // SQL function compared to column: LEN(p.Name) > p.MinLength
            | MethodCall m, Column (p, _) ->
                let sqlFragment = visitSqlFn qualifyColumn (m :> Expression)
                let comparison = getComparison exp.NodeType
                let alias = visitAlias p.Expression
                let fqCol = qualifyColumn alias p.Member
                query.WhereRaw($"{sqlFragment} {comparison} {fqCol}")

            // Column compared to SQL function: p.MinLength < LEN(p.Name)
            | Column (p, _), MethodCall m ->
                let alias = visitAlias p.Expression
                let fqCol = qualifyColumn alias p.Member
                let sqlFragment = visitSqlFn qualifyColumn (m :> Expression)
                let comparison = getComparison exp.NodeType
                query.WhereRaw($"{fqCol} {comparison} {sqlFragment}")

            // SQL function compared to SQL function: LEN(p.First) > LEN(p.Last)
            | MethodCall m1, MethodCall m2 ->
                let sqlFragment1 = visitSqlFn qualifyColumn (m1 :> Expression)
                let sqlFragment2 = visitSqlFn qualifyColumn (m2 :> Expression)
                let comparison = getComparison exp.NodeType
                query.WhereRaw($"{sqlFragment1} {comparison} {sqlFragment2}")

            | Value v1, Value v2 ->
                notImplMsg("Value to value comparisons are not currently supported. Ex: where (1 = 1)")
            | _ ->
                notImpl()
        | _ ->
            notImpl()

    visit (filter :> Expression) (Query())

let visitHaving<'T> (tables: TableMapping seq) (filter: Expression<Func<'T, bool>>) (qualifyColumn: string -> MemberInfo -> string) =
    /// A column/property on a mapped table/record.
    let (|Column|_|) (exp: Expression) = 
        match exp with
        | MappedColumn tables (p, ext) -> Some (p, ext)
        | _ -> None

    let rec visit (exp: Expression) (query: Query) : Query =
        match exp with
        | Lambda x -> visit x.Body query
        | Not operand -> 
            let operand = visit operand (Query())
            query.HavingNot(fun q -> operand)
        | MethodCall m when m.Method.Name = "Invoke" ->
            // Handle tuples
            visit m.Object (Query())
        | MethodCall m when List.contains m.Method.Name [ nameof isIn; nameof isNotIn; nameof op_BarEqualsBar; nameof op_BarLessGreaterBar ] ->
            let filter : (string * seq<obj>) -> Query = 
                match m.Method.Name with
                | nameof isIn | nameof op_BarEqualsBar -> query.HavingIn
                | _ -> query.HavingNotIn

            match m.Arguments.[0], m.Arguments.[1] with
            // Column is IN / NOT IN a subquery of values
            | Column (p, _), MethodCall subqueryExpr when subqueryExpr.Method.Name = nameof subqueryMany ->
                let subqueryConst = match subqueryExpr.Arguments.[0] with | Constant c -> c | _ -> notImpl()
                let alias = visitAlias p.Expression
                let fqCol = qualifyColumn alias p.Member
                let selectSubquery = subqueryConst.Value :?> SelectQuery
                match m.Method.Name with
                | nameof isIn | nameof op_BarEqualsBar -> query.HavingIn(fqCol, selectSubquery.ToKataQuery())
                | _ -> query.HavingNotIn(fqCol, selectSubquery.ToKataQuery())
            // Column is IN / NOT IN a list of values
            | Column (p, _), ListInit values ->
                let queryParameters = 
                    values 
                    |> Seq.map (KataUtils.getQueryParameterForValue p.Member)
                    |> Seq.toArray

                let alias = visitAlias p.Expression
                let fqCol = qualifyColumn alias p.Member
                filter(fqCol, queryParameters)
            // Column is IN / NOT IN an array of values
            | Column (p, _), ArrayInit values -> 
                let queryParameters = 
                    values 
                    |> Seq.map (KataUtils.getQueryParameterForValue p.Member)
                    |> Seq.toArray

                let alias = visitAlias p.Expression
                let fqCol = qualifyColumn alias p.Member
                filter(fqCol, queryParameters)
            // Column is IN / NOT IN an IEnumerable of values
            | Column (p, _), Value value -> 
                let queryParameters = 
                    (value :?> System.Collections.IEnumerable) 
                    |> Seq.cast<obj> 
                    |> Seq.map (KataUtils.getQueryParameterForValue p.Member)
                    |> Seq.toArray

                let alias = visitAlias p.Expression
                let fqCol = qualifyColumn alias p.Member
                filter(fqCol, queryParameters)
            // Column is IN / NOT IN a sequence expression of values
            | Column (p, _), MethodCall c when c.Method.Name = "CreateSequence" ->
                notImplMsg "Unable to unwrap sequence expression. Please use a list or array instead."
            | _ -> notImpl()
        | MethodCall m when List.contains m.Method.Name [ nameof like; nameof notLike; nameof op_EqualsPercent; nameof op_LessGreaterPercent ] ->
            match m.Arguments.[0], m.Arguments.[1] with
            | Column (p, _), Value value -> 
                let pattern = string value
                match m.Method.Name with
                | nameof like | nameof op_EqualsPercent -> 
                    let alias = visitAlias p.Expression
                    let fqCol = qualifyColumn alias p.Member
                    query.HavingLike(fqCol, pattern, false)
                | _ -> 
                    let alias = visitAlias p.Expression
                    let fqCol = qualifyColumn alias p.Member
                    query.HavingNotLike(fqCol, pattern, false)
            | _ -> notImpl()
        | MethodCall m when m.Method.Name = nameof isNullValue || m.Method.Name = nameof isNotNullValue ->
            match m.Arguments.[0] with
            | Column (p, _) -> 
                let alias = visitAlias p.Expression
                let fqCol = qualifyColumn alias p.Member
                if m.Method.Name = nameof isNullValue
                then query.HavingNull(fqCol)
                else query.HavingNotNull(fqCol)
            | _ -> notImpl()
        | MethodCall m when List.contains m.Method.Name [ nameof minBy; nameof maxBy; nameof sumBy; nameof avgBy; nameof countBy; nameof avgByAs ] ->
            // Handle aggregate columns
            visit m.Arguments.[0] query
        | BinaryAnd x ->
            let lt = visit x.Left (Query())
            let rt = visit x.Right (Query())
            query.Having(fun q -> lt).Having(fun q -> rt)
        | BinaryOr x -> 
            let lt = visit x.Left (Query())
            let rt = visit x.Right (Query())
            query.OrHaving(fun q -> lt).OrHaving(fun q -> rt)
        | BinaryCompare x ->
            match x.Left, x.Right with            
            | Column (p1, _), MethodCall subqueryExpr when subqueryExpr.Method.Name = nameof subqueryOne ->
                // Handle property to subquery comparisons
                let comparison = getComparison exp.NodeType
                let subqueryConst = match subqueryExpr.Arguments.[0] with | Constant c -> c | _ -> notImpl()
                let selectSubquery = subqueryConst.Value :?> SelectQuery
                let alias = visitAlias p1.Expression
                let fqCol = qualifyColumn alias p1.Member
                query.Having(fqCol, comparison, selectSubquery.ToKataQuery())
            | AggregateColumn (aggType, (p1, _)), Column (p2, _) ->
                // Handle aggregate col to col comparisons
                let lt = 
                    let alias = visitAlias p1.Expression
                    qualifyColumn alias p1.Member
                let comparison = getComparison exp.NodeType
                let rt = 
                    let alias = visitAlias p2.Expression
                    qualifyColumn alias p2.Member
                query.HavingRaw($"{aggType}({lt}) {comparison} {rt}")
            | AggregateColumn (aggType, (p, _)), Value value ->
                // Handle aggregate column to value comparisons
                let alias = visitAlias p.Expression
                let lt = qualifyColumn alias p.Member
                let comparison = getComparison(exp.NodeType)
                query.HavingRaw($"{aggType}({lt}) {comparison} ?", [value])
            | Column (p1, _), Column (p2, _) ->
                // Handle col to col comparisons
                let lt = 
                    let alias = visitAlias p1.Expression
                    qualifyColumn alias p1.Member
                let comparison = getComparison exp.NodeType
                let rt = 
                    let alias = visitAlias p2.Expression
                    qualifyColumn alias p2.Member
                query.HavingColumns(lt, comparison, rt)
            | Column (p, _), Value value ->
                // Handle column to value comparisons
                match exp.NodeType, value with
                | ExpressionType.Equal, null -> 
                    let alias = visitAlias p.Expression
                    query.WhereNull(qualifyColumn alias p.Member)
                | ExpressionType.NotEqual, null -> 
                    let alias = visitAlias p.Expression
                    query.WhereNotNull(qualifyColumn alias p.Member)
                | _ ->                     
                    let comparison = getComparison(exp.NodeType)
                    let queryParameter = KataUtils.getQueryParameterForValue p.Member value
                    let alias = visitAlias p.Expression
                    query.Where(qualifyColumn alias p.Member, comparison, queryParameter)
            | Value _, Value _ ->
                // Not implemented because I didn't want to embed logic to properly format strings, dates, etc.
                // This can be easily added later if it is implemented in Dapper.FSharp.
                notImplMsg("Value to value comparisons are not currently supported. Ex: having (1 = 1)")
            | _ ->
                notImpl()
        | _ ->
            notImpl()

    visit (filter :> Expression) (Query())

/// Returns a list of one or more fully qualified column names: ["{schema}.{table}.{column}"]
let visitPropertiesSelector<'T, 'Prop> (propertySelector: Expression<Func<'T, 'Prop>>) (qualifyColumn: string -> MemberInfo -> string) =
    let rec visit (exp: Expression) : string list =
        match exp with
        | Lambda x -> visit x.Body
        | MethodCall m when m.Method.Name = "Invoke" ->
            // Handle tuples
            visit m.Object
        | New n -> 
            // Handle groupBy that returns a tuple of multiple columns
            n.Arguments |> Seq.map visit |> Seq.toList |> List.concat
        | Member m -> 
            // Handle groupBy for a single column
            let alias = visitAlias m.Expression
            let column = qualifyColumn alias m.Member
            [column]
        | _ -> notImpl()

    visit (propertySelector :> Expression)

type OrderBy =
    | OrderByColumn of tableAlias: string * MemberInfo
    | OrderByAggregateColumn of aggregateType: string * tableAlias: string * MemberInfo
    | OrderByIgnored

/// Returns a column MemberInfo.
let visitOrderByPropertySelector<'T, 'Prop> (propertySelector: Expression<Func<'T, 'Prop>>) =
    let rec visit (exp: Expression) : OrderBy =
        match exp with
        | Lambda x -> visit x.Body
        | MethodCall m when m.Method.Name = "Invoke" ->
            // Handle tuples
            visit m.Object
        | MethodCall m when m.Method.Name = nameof op_HatHat ->
            // ^^ operator conditionally adds property to order by clause
            match m.Arguments[0], m.Arguments[1] with
            | Value enabled, Property (p, _) ->
                if enabled :?> bool then 
                    let alias = visitAlias p.Expression
                    OrderByColumn (alias, p.Member)
                else
                    OrderByIgnored
            | _ -> 
                notImpl()            
        | AggregateColumn (aggType, (p, _)) -> 
            let alias = visitAlias p.Expression
            OrderByAggregateColumn (aggType, alias, p.Member)
        | Member m -> 
            if m.Member.DeclaringType |> isOptionOrNullableType then 
                visit m.Expression
            else 
                let alias = visitAlias m.Expression
                OrderByColumn (alias, m.Member)
        | Property (p, _) -> 
            let alias = visitAlias p.Expression
            OrderByColumn (alias, p.Member)
        | _ -> notImpl()

    visit (propertySelector :> Expression)

type JoinedPropertyInfo = 
    {
        Alias: string
        Member: MemberInfo
    }

/// Returns one or more column members
let visitJoin<'T, 'Prop> (propertySelector: Expression<Func<'T, 'Prop>>) =
    let rec visit (exp: Expression) : JoinedPropertyInfo list =
        match exp with
        | Lambda x -> visit x.Body
        | MethodCall m when m.Method.Name = "Invoke" ->
            // Handle tuples
            visit m.Object
        | New n -> 
            // Handle groupBy that returns a tuple of multiple columns
            n.Arguments |> Seq.map visit |> Seq.toList |> List.collect id
        | Member m -> 
            let alias = visitAlias m.Expression
            if m.Member.DeclaringType |> isOptionOrNullableType
            then visit m.Expression
            else [ { Alias = alias; Member = m.Member } ]
        | Property (p, _) -> 
            let alias = visitAlias p.Expression
            [ { Alias = alias; Member = p.Member }  ]
        | _ -> notImpl()

    visit (propertySelector :> Expression)

/// Returns a column MemberInfo.
let visitPropertySelector<'T, 'Prop> (propertySelector: Expression<Func<'T, 'Prop>>) =
    let rec visit (exp: Expression) : MemberInfo =
        match exp with
        | Lambda x -> visit x.Body
        | MethodCall m when m.Method.Name = "Invoke" ->
            // Handle tuples
            visit m.Object
        | Member m -> 
            if m.Member.DeclaringType |> isOptionOrNullableType
            then visit m.Expression
            else m.Member
        | Property (p, _) -> p.Member
        | _ -> notImpl()

    visit (propertySelector :> Expression)

type Selection =
    | SelectedTable of tableAlias: string * tableType: Type
    | SelectedColumn of tableAlias: string * column: string * columnType: Type * isOpt: bool * isNullable: bool
    | SelectedExpression of sqlFragment: string

/// A database-sourced leaf in a selectExpr expression tree.
type ExprLeaf =
    | TableLeaf of tableAlias: string * tableType: Type * index: int
    | ColumnLeaf of tableAlias: string * column: string * columnType: Type
                   * isOpt: bool * isNullable: bool * index: int
    | SqlExprLeaf of sqlFragment: string * resultType: Type * alias: string * index: int

/// Result of visiting a selectExpr expression (Pass 1).
type SelectExprInfo = {
    Leaves: ExprLeaf list
    LeafTupleType: Type
    CompiledMapper: Func<obj[], obj>
}

module SelectExprStore =
    open System.Runtime.CompilerServices

    let private store = ConditionalWeakTable<SqlKata.Query, SelectExprInfo>()

    let set (query: SqlKata.Query) (info: SelectExprInfo) =
        store.Remove(query) |> ignore
        store.Add(query, info)

    let tryGet (query: SqlKata.Query) =
        match store.TryGetValue(query) with
        | true, info -> Some info
        | _ -> None

/// Visits a join predicate expression and builds SqlKata Join.On() calls.
/// Used by the `on'` operation to support predicate-style joins.
let visitJoinPredicate<'T> (tables: TableMapping seq) (predicate: Expression<Func<'T, bool>>) (qualifyColumn: string -> MemberInfo -> string) =
    /// A column/property on a mapped table/record.
    let (|Column|_|) (exp: Expression) =
        match exp with
        | MappedColumn tables (p, ext) -> Some (p, ext)
        | _ -> None

    let rec visit (exp: Expression) (j: SqlKata.Join) : SqlKata.Join =
        match exp with
        | Lambda x -> visit x.Body j
        | MethodCall m when m.Method.Name = "Invoke" ->
            // Handle tuples
            visit m.Object j
        | BinaryAnd x ->
            // Visit left and right sides, chaining .On() calls
            let j' = visit x.Left j
            visit x.Right j'
        | BinaryOr x ->
            // For OR conditions, we need to use OrOn
            let leftJoin = visit x.Left (SqlKata.Join())
            let rightJoin = visit x.Right (SqlKata.Join())
            j.Where(fun _ -> leftJoin).OrWhere(fun _ -> rightJoin)
        | BinaryCompare x ->
            match x.Left, x.Right with
            // Handle col to col comparisons (the primary join case)
            | Column (p1, ext1), Column (p2, ext2) ->
                let lt =
                    let alias = visitAlias p1.Expression
                    qualifyColumn alias p1.Member
                let comparison = getComparison exp.NodeType
                let rt =
                    let alias = visitAlias p2.Expression
                    qualifyColumn alias p2.Member
                j.On(lt, rt, comparison)

            // Handle column to value comparisons (e.g., d.Type = "SomeValue")
            | Column (p, ext), Value value ->
                let alias = visitAlias p.Expression
                let fqCol = qualifyColumn alias p.Member
                let comparison = getComparison exp.NodeType
                match value with
                | null when comparison = "=" ->
                    j.WhereNull(fqCol)
                | null when comparison = "<>" ->
                    j.WhereNotNull(fqCol)
                | _ ->
                    let queryParameter = KataUtils.getQueryParameterForValue p.Member value
                    j.Where(fqCol, comparison, queryParameter)

            // Handle value to column comparisons (reversed)
            | Value value, Column (p, ext) ->
                let alias = visitAlias p.Expression
                let fqCol = qualifyColumn alias p.Member
                let comparison = getReverseComparison exp.NodeType
                match value with
                | null when comparison = "=" ->
                    j.WhereNull(fqCol)
                | null when comparison = "<>" ->
                    j.WhereNotNull(fqCol)
                | _ ->
                    let queryParameter = KataUtils.getQueryParameterForValue p.Member value
                    j.Where(fqCol, comparison, queryParameter)

            // Nullable.Value / Option.Value comparisons
            | Column (p, ext), _ when ext = ExtProperty.Value ->
                let value = x.Right |> compileAndEvaluateExpression
                let comparison = getComparison exp.NodeType
                let alias = visitAlias p.Expression
                let m = tryGetMember p
                let fqCol = qualifyColumn alias m.Value.Member
                match value with
                | null when comparison = "=" ->
                    j.WhereNull(fqCol)
                | null when comparison = "<>" ->
                    j.WhereNotNull(fqCol)
                | _ ->
                    let queryParameter = KataUtils.getQueryParameterForValue p.Member value
                    j.Where(fqCol, comparison, queryParameter)

            | _ ->
                notImplMsg $"Unsupported join predicate comparison: {x.Left.NodeType} {exp.NodeType} {x.Right.NodeType}"
        | _ ->
            notImplMsg $"Unsupported join predicate expression: {exp.NodeType}"

    fun (j: SqlKata.Join) -> visit (predicate :> Expression) j

/// Returns a list of one or more fully qualified table names: ["{schema}.{table}"]
let visitSelect<'T, 'Prop> (propertySelector: Expression<Func<'T, 'Prop>>) =
    let rec visit (exp: Expression) : Selection list =
        match exp with
        | Lambda x -> visit x.Body
        | MethodCall m when m.Method.Name = "Invoke" ->
            // Handle tuples
            visit m.Object
        | MethodCall m when m.Method.Name = "Some" ->
            // Columns selected from leftJoined tables may be wrapped in `Some` to make them optional.
            visit m.Arguments.[0]
        | MethodCall m when m.Method.Name = "op_PipeRight" && m.Arguments.Count = 2 ->
            // Handle: r |> Option.map _.ColumnA
            // The F# compiler generates: op_PipeRight(source, Lambda(...ToFSharpFunc(Lambda(Option.Map(...)))).Invoke(ToFSharpFunc(Lambda(mapping))))
            // We extract the source from Arg0 and the mapping lambda from the Invoke argument.
            let source = m.Arguments.[0]
            let rec findOptionMapLambda (exp: Expression) =
                match exp with
                | :? MethodCallExpression as invoke when invoke.Method.Name = "Invoke" ->
                    // The Invoke argument contains the mapping function wrapped in ToFSharpFunc(Lambda(...))
                    match invoke.Arguments.[0] with
                    | :? MethodCallExpression as toFF when toFF.Method.Name = "ToFSharpFunc" ->
                        match toFF.Arguments.[0] with
                        | :? LambdaExpression as mapLam -> Some mapLam
                        | _ -> None
                    | _ -> None
                | _ -> None
            // Verify this is actually wrapping OptionModule.Map by checking the lambda body chain
            let rec containsOptionMap (exp: Expression) =
                match exp with
                | :? MethodCallExpression as mc ->
                    mc.Method.Name = "Map" && mc.Method.DeclaringType <> null && mc.Method.DeclaringType.Name = "OptionModule"
                    || mc.Arguments |> Seq.exists containsOptionMap
                    || (mc.Object <> null && containsOptionMap mc.Object)
                | :? LambdaExpression as lam -> containsOptionMap lam.Body
                | _ -> false
            if containsOptionMap m.Arguments.[1] then
                match findOptionMapLambda m.Arguments.[1] with
                | Some mapLam ->
                    match mapLam.Body with
                    | Member memberExp ->
                        let alias = visitAlias source
                        [ SelectedColumn (alias, memberExp.Member.Name, memberExp.Type, true, false) ]
                    | _ -> notImplMsg $"Unsupported Option.map lambda body: {mapLam.Body.NodeType}"
                | None -> notImplMsg $"Could not extract mapping lambda from Option.map expression"
            else
                // Not an Option.map pipe; fall through to generic SQL function handling
                let qualifyCol alias (mem: MemberInfo) = $"{{%s{alias}}}.{{%s{mem.Name}}}"
                let sqlFragment = visitSqlFn qualifyCol (m :> Expression)
                [ SelectedExpression sqlFragment ]
        | AggregateColumn (aggType, (p, _)) ->
            let alias = visitAlias p.Expression
            let fqCol = $"{{%s{alias}}}.{{%s{p.Member.Name}}}"
            [ SelectedExpression $"{aggType}({fqCol})" ]
        | MethodCall m ->
            // Treat any other method call as a SQL function
            let qualifyCol alias (mem: MemberInfo) = $"{{%s{alias}}}.{{%s{mem.Name}}}"
            let sqlFragment = visitSqlFn qualifyCol (m :> Expression)
            [ SelectedExpression sqlFragment ]
        | New n -> 
            // Handle a tuple of multiple tables
            n.Arguments 
            |> Seq.map visit |> Seq.toList |> List.concat
        | Parameter p -> 
            [ SelectedTable (p.Name, p.Type) ]
        | Member m -> 
            if m.Member.DeclaringType |> isOptionOrNullableType then 
                visit m.Expression
            else 
                let isOptional, isNullable =
                    if m.Type.IsGenericType && m.Type.GetGenericTypeDefinition() = typedefof<Option<_>> then true, false
                    elif m.Type.IsGenericType && m.Type.GetGenericTypeDefinition() = typedefof<Nullable<_>> then false, true
                    else false, false

                let alias = visitAlias m.Expression
                [ SelectedColumn (alias, m.Member.Name, m.Type, isOptional, isNullable) ]
        | _ ->
            notImpl()

    visit (propertySelector :> Expression)

/// Tracks how each alias is used in the projection expression.
type AliasUsage = {
    mutable RequiresFullRecord: bool
    UsedColumns: System.Collections.Generic.HashSet<string>
}

/// Analyzes a projection expression to determine, per alias, whether the full record is needed
/// or only specific columns are accessed.
let analyzeProjectionShape
    (body: Expression)
    (outerParamNames: System.Collections.Generic.HashSet<string>)
    (outerParamTypes: System.Collections.Generic.Dictionary<string, Type>)
    (typeToAlias: System.Collections.Generic.Dictionary<Type, string>)
    =
    let usageMap = System.Collections.Generic.Dictionary<string, AliasUsage>()

    let getOrCreate (alias: string) =
        match usageMap.TryGetValue(alias) with
        | true, u -> u
        | false, _ ->
            let u = { RequiresFullRecord = false; UsedColumns = System.Collections.Generic.HashSet<string>() }
            usageMap.[alias] <- u
            u

    let isGeneratedRecordType (t: Type) =
        t <> null && t.DeclaringType <> null && FSharp.Reflection.FSharpType.IsRecord(t, System.Reflection.BindingFlags.Public ||| System.Reflection.BindingFlags.NonPublic)

    let isOptionOfGeneratedRecordType (t: Type) =
        t <> null && isOptionType t && isGeneratedRecordType (t.GetGenericArguments().[0])

    let isTableType (t: Type) =
        isGeneratedRecordType t || isOptionOfGeneratedRecordType t

    /// Resolve alias from a parameter expression using the same logic as the rewriter.
    let resolveAlias (p: ParameterExpression) =
        if outerParamNames.Contains(p.Name) then p.Name
        else
            match typeToAlias.TryGetValue(p.Type) with
            | true, alias -> alias
            | false, _ -> p.Name

    /// Try to get the alias from an expression that bottoms out at a parameter.
    let rec tryGetAlias (e: Expression) =
        match e with
        | Parameter p when isTableType p.Type -> Some (resolveAlias p)
        | Member m when m.Member.DeclaringType <> null && isOptionOrNullableType m.Member.DeclaringType ->
            tryGetAlias m.Expression
        | _ -> None

    /// Check if a MethodCall is an Option.map/Option.bind with a simple field accessor lambda.
    /// Returns Some(sourceExpr, fieldName) if so.
    let (|OptionMapField|_|) (exp: Expression) =
        match exp with
        | MethodCall m when m.Method.Name = "op_PipeRight" && m.Arguments.Count = 2 ->
            let source = m.Arguments.[0]
            let rec containsOptionMap (e: Expression) =
                match e with
                | :? MethodCallExpression as mc ->
                    (mc.Method.Name = "Map" && mc.Method.DeclaringType <> null && mc.Method.DeclaringType.Name = "OptionModule")
                    || mc.Arguments |> Seq.exists containsOptionMap
                    || (mc.Object <> null && containsOptionMap mc.Object)
                | :? LambdaExpression as lam -> containsOptionMap lam.Body
                | _ -> false
            if containsOptionMap m.Arguments.[1] then
                let rec findMapLambda (e: Expression) =
                    match e with
                    | :? MethodCallExpression as invoke when invoke.Method.Name = "Invoke" ->
                        match invoke.Arguments.[0] with
                        | :? MethodCallExpression as toFF when toFF.Method.Name = "ToFSharpFunc" ->
                            match toFF.Arguments.[0] with
                            | :? LambdaExpression as mapLam -> Some mapLam
                            | _ -> None
                        | _ -> None
                    | _ -> None
                match findMapLambda m.Arguments.[1] with
                | Some mapLam ->
                    match mapLam.Body with
                    | Member memberExp -> Some (source, memberExp.Member.Name)
                    | _ -> None
                | None -> None
            else None
        | _ -> None

    let rec analyze (exp: Expression) =
        match exp with
        // Option.map _.Field — only needs the specific column
        | OptionMapField (source, fieldName) ->
            match tryGetAlias source with
            | Some alias -> (getOrCreate alias).UsedColumns.Add(fieldName) |> ignore
            | None -> analyzeChildren exp

        // Match/switch scrutinee — marks full record required
        | :? SwitchExpression as sw ->
            match tryGetAlias sw.SwitchValue with
            | Some alias -> (getOrCreate alias).RequiresFullRecord <- true
            | None -> ()
            analyzeChildren exp

        // Conditional — check if test is on an alias (match on option compiles to conditional)
        | :? ConditionalExpression as c ->
            // For `match optAlias with Some x -> ... | None -> ...`, the test is typically
            // a check like `optAlias.get_Tag() == 1` or similar. We detect if the test references a table alias.
            analyzeConditionalTest c.Test
            analyze c.IfTrue
            analyze c.IfFalse

        // Direct member access on a table parameter: alias.Field
        | Member m when m.Expression <> null ->
            match tryGetAlias m.Expression with
            | Some alias when not (isOptionOrNullableType m.Member.DeclaringType) ->
                (getOrCreate alias).UsedColumns.Add(m.Member.Name) |> ignore
            | _ -> analyzeChildren exp

        // A standalone table-typed parameter (not accessed as .Field or via Option.map)
        | Parameter p when isTableType p.Type ->
            let alias = resolveAlias p
            (getOrCreate alias).RequiresFullRecord <- true

        | _ -> analyzeChildren exp

    and analyzeChildren (exp: Expression) =
        match exp with
        | Lambda x -> analyze x.Body
        | MethodCall m ->
            if m.Object <> null then analyze m.Object
            for arg in m.Arguments do analyze arg
        | :? InvocationExpression as inv ->
            analyze inv.Expression
            for arg in inv.Arguments do analyze arg
        | New n ->
            for arg in n.Arguments do analyze arg
        | Unary u -> analyze u.Operand
        | Binary b -> analyze b.Left; analyze b.Right
        | :? ConditionalExpression as c ->
            analyze c.Test; analyze c.IfTrue; analyze c.IfFalse
        | :? BlockExpression as blk ->
            for e in blk.Expressions do analyze e
        | :? SwitchExpression as sw ->
            analyze sw.SwitchValue
            if sw.DefaultBody <> null then analyze sw.DefaultBody
            for case in sw.Cases do
                for tv in case.TestValues do analyze tv
                analyze case.Body
        | _ -> ()

    and analyzeConditionalTest (exp: Expression) =
        // Walk the test expression looking for table-typed parameter references.
        // If a table-typed parameter is used in the test of a conditional (match expression),
        // it requires the full record.
        match exp with
        | MethodCall m ->
            // Check if any argument is a table-typed parameter access
            let mutable found = false
            if m.Object <> null then
                match tryGetAlias m.Object with
                | Some alias -> (getOrCreate alias).RequiresFullRecord <- true; found <- true
                | None -> ()
            for arg in m.Arguments do
                match tryGetAlias arg with
                | Some alias -> (getOrCreate alias).RequiresFullRecord <- true; found <- true
                | None -> ()
            if not found then
                if m.Object <> null then analyzeConditionalTest m.Object
                for arg in m.Arguments do analyzeConditionalTest arg
        | Member m when m.Expression <> null ->
            match tryGetAlias m.Expression with
            | Some alias when isOptionOrNullableType m.Member.DeclaringType || m.Member.Name = "get_Tag" || m.Member.Name = "Tag" ->
                (getOrCreate alias).RequiresFullRecord <- true
            | _ -> analyzeConditionalTest m.Expression
        | Unary u -> analyzeConditionalTest u.Operand
        | Binary b -> analyzeConditionalTest b.Left; analyzeConditionalTest b.Right
        | _ -> ()

    analyze body
    usageMap

/// Visits a selectExpr expression in two passes:
/// Pass 1: Walk the expression tree to identify database leaf sub-expressions and rewrite the tree.
/// Pass 2 (at runtime): Use the compiled mapper with leaf values to produce the final result.
let visitSelectExpr<'T, 'Selected> (selectExpression: Expression<Func<'T, 'Selected>>) =
    let leaves = ResizeArray<ExprLeaf>()
    let leafIndex = ref 0
    // Deduplication: (tableAlias.column) -> index
    let leafKeys = System.Collections.Generic.Dictionary<string, int>()
    let sqlExprCounter = ref 0

    let paramArray = Expression.Parameter(typeof<obj[]>, "leafValues")

    let getOrAddLeaf (key: string) (mkLeaf: int -> ExprLeaf) (leafType: Type) =
        match leafKeys.TryGetValue(key) with
        | true, existingIdx ->
            // Return array access for existing leaf
            Expression.Convert(
                Expression.ArrayIndex(paramArray, Expression.Constant(existingIdx)),
                leafType) :> Expression
        | false, _ ->
            let idx = !leafIndex
            leafIndex := idx + 1
            let leaf = mkLeaf idx
            leaves.Add(leaf)
            leafKeys.[key] <- idx
            Expression.Convert(
                Expression.ArrayIndex(paramArray, Expression.Constant(idx)),
                leafType) :> Expression

    /// Checks if a type is a generated record type (has a declaring type, indicating it's a nested type under a schema module).
    let isGeneratedRecordType (t: Type) =
        t <> null && t.DeclaringType <> null && FSharp.Reflection.FSharpType.IsRecord(t, System.Reflection.BindingFlags.Public ||| System.Reflection.BindingFlags.NonPublic)

    /// Checks if a type is Option<RecordType> where RecordType is a generated record type.
    let isOptionOfGeneratedRecordType (t: Type) =
        t <> null && isOptionType t && isGeneratedRecordType (t.GetGenericArguments().[0])

    /// Checks if a type is a generated record type or an Option wrapping one.
    let isTableType (t: Type) =
        isGeneratedRecordType t || isOptionOfGeneratedRecordType t

    /// Checks if a method belongs to the SqlHydra.Query assembly (i.e., is a SQL function).
    let isSqlHydraMethod (m: MethodInfo) =
        m.Module.Name = "SqlHydra.Query.dll"

    // --- Alias resolution dictionaries ---
    // Primary: maps inner lambda ParameterExpressions to their resolved outer alias
    let paramToAlias = System.Collections.Generic.Dictionary<ParameterExpression, string>()
    // Fallback: maps types (both Option<T> and T) to alias. Known limitation: if two outer
    // params share the same inner type, the last one wins.
    let typeToAlias = System.Collections.Generic.Dictionary<Type, string>()
    // Names of outer parameters (the table aliases like p, o, sr, r)
    let outerParamNames = System.Collections.Generic.HashSet<string>()
    // Maps outer param alias to its declared type (for determining optionality)
    let outerParamTypes = System.Collections.Generic.Dictionary<string, Type>()
    // Projection shape analysis result (populated after unwrapBodyWithParams)
    let mutable aliasUsage = System.Collections.Generic.Dictionary<string, AliasUsage>()

    /// Unwrap the Lambda -> Invoke -> Lambda nesting that F# CEs generate,
    /// collecting outer parameters along the way.
    let rec unwrapBodyWithParams (exp: Expression) =
        match exp with
        | Lambda x ->
            // Collect outer lambda parameters (these are the table aliases)
            for p in x.Parameters do
                outerParamNames.Add(p.Name) |> ignore
                outerParamTypes.[p.Name] <- p.Type
                // Register type mappings for fallback resolution
                if isTableType p.Type then
                    typeToAlias.[p.Type] <- p.Name
                    if isOptionType p.Type then
                        typeToAlias.[p.Type.GetGenericArguments().[0]] <- p.Name
            unwrapBodyWithParams x.Body
        | MethodCall m when m.Method.Name = "Invoke" -> unwrapBodyWithParams m.Object
        | _ -> exp

    /// Resolve the table alias for a ParameterExpression.
    let resolveTableAlias (p: ParameterExpression) =
        if outerParamNames.Contains(p.Name) then p.Name
        else
            match paramToAlias.TryGetValue(p) with
            | true, alias -> alias
            | false, _ ->
                // Fallback: look up by type
                match typeToAlias.TryGetValue(p.Type) with
                | true, alias -> alias
                | false, _ -> p.Name

    /// Check if an expression resolves to a table-typed parameter.
    let rec isTableExpr (e: Expression) =
        match e with
        | Parameter p -> isTableType p.Type
        | Member inner when inner.Member.DeclaringType <> null && inner.Member.DeclaringType |> isOptionOrNullableType ->
            isTableExpr inner.Expression
        | _ -> false

    /// Resolve alias from an arbitrary expression that bottoms out at a parameter.
    let rec resolveAliasFromExpr (e: Expression) =
        match e with
        | Parameter p -> resolveTableAlias p
        | Member inner when inner.Member.DeclaringType <> null && inner.Member.DeclaringType |> isOptionOrNullableType ->
            resolveAliasFromExpr inner.Expression
        | _ -> visitAlias e

    /// Returns the table alias if the expression has provenance from a CE table parameter.
    let rec getProvenance (e: Expression) : string option =
        match e with
        | Parameter p ->
            if outerParamNames.Contains(p.Name) then Some p.Name
            else
                match paramToAlias.TryGetValue(p) with
                | true, alias -> Some alias
                | false, _ -> None
        | Member m when m.Member.DeclaringType <> null && isOptionOrNullableType m.Member.DeclaringType ->
            getProvenance m.Expression
        | _ -> None

    let rec rewrite (exp: Expression) : Expression =
        match exp with
        // --- SQL leaf detection (before generic cases) ---
        | AggregateColumn (aggType, (p, _)) ->
            let alias = visitAlias p.Expression
            let fqCol = $"[{alias}].[{p.Member.Name}]" // NOTE: SqlKata will translate [ ] to proper quoting for the target dialect.
            let sqlFragment = $"{aggType}({fqCol})"
            let exprAlias = $"__hydra_expr_{!sqlExprCounter}"
            sqlExprCounter := !sqlExprCounter + 1
            let key = $"__sqlfn:{sqlFragment}"
            getOrAddLeaf key
                (fun idx -> SqlExprLeaf (sqlFragment, exp.Type, exprAlias, idx))
                exp.Type

        | MethodCall m when isSqlHydraMethod m.Method ->
            rewriteSqlFunction m exp

        // --- Option.map _.Field pattern (column-only when analysis says so) ---
        | MethodCall m when m.Method.Name = "op_PipeRight" && m.Arguments.Count = 2 ->
            let source = m.Arguments.[0]
            let rec containsOptionMap (e: Expression) =
                match e with
                | :? MethodCallExpression as mc ->
                    (mc.Method.Name = "Map" && mc.Method.DeclaringType <> null && mc.Method.DeclaringType.Name = "OptionModule")
                    || mc.Arguments |> Seq.exists containsOptionMap
                    || (mc.Object <> null && containsOptionMap mc.Object)
                | :? LambdaExpression as lam -> containsOptionMap lam.Body
                | _ -> false
            if containsOptionMap m.Arguments.[1] then
                // Try to extract the field accessor lambda
                let rec findMapLambda (e: Expression) =
                    match e with
                    | :? MethodCallExpression as invoke when invoke.Method.Name = "Invoke" ->
                        match invoke.Arguments.[0] with
                        | :? MethodCallExpression as toFF when toFF.Method.Name = "ToFSharpFunc" ->
                            match toFF.Arguments.[0] with
                            | :? LambdaExpression as mapLam -> Some mapLam
                            | _ -> None
                        | _ -> None
                    | _ -> None
                // Resolve source alias
                let sourceAlias =
                    let rec tryResolve (e: Expression) =
                        match e with
                        | Parameter p -> Some (resolveTableAlias p)
                        | Member inner when inner.Member.DeclaringType <> null && isOptionOrNullableType inner.Member.DeclaringType ->
                            tryResolve inner.Expression
                        | _ -> None
                    tryResolve source
                match sourceAlias, findMapLambda m.Arguments.[1] with
                | Some alias, Some mapLam when
                    aliasUsage.ContainsKey(alias) && not aliasUsage.[alias].RequiresFullRecord ->
                    match mapLam.Body with
                    | Member memberExp ->
                        // Column-only: register a ColumnLeaf with isOpt=true (comes through Option.map)
                        let isOuterOptional =
                            match outerParamTypes.TryGetValue(alias) with
                            | true, outerType -> isOptionType outerType
                            | false, _ -> false
                        let colType =
                            if isOuterOptional then
                                typedefof<Option<_>>.MakeGenericType(memberExp.Type)
                            else
                                memberExp.Type
                        let key = $"{alias}.{memberExp.Member.Name}"
                        getOrAddLeaf key
                            (fun idx -> ColumnLeaf (alias, memberExp.Member.Name, colType, true, false, idx))
                            colType
                    | _ ->
                        // Fall through: rewrite children
                        let newArgs = m.Arguments |> Seq.map rewrite |> Seq.toArray
                        let newObj = if m.Object <> null then rewrite m.Object else null
                        let argsChanged = Seq.zip m.Arguments newArgs |> Seq.exists (fun (a, b) -> not (obj.ReferenceEquals(a, b)))
                        let objChanged = m.Object <> null && not (obj.ReferenceEquals(newObj, m.Object))
                        if argsChanged || objChanged then
                            if m.Object <> null then Expression.Call(newObj, m.Method, newArgs) :> Expression
                            else Expression.Call(m.Method, newArgs) :> Expression
                        else exp
                | _ ->
                    // Full record needed or can't resolve: fall through to generic MethodCall rewrite
                    let newArgs = m.Arguments |> Seq.map rewrite |> Seq.toArray
                    let newObj = if m.Object <> null then rewrite m.Object else null
                    let argsChanged = Seq.zip m.Arguments newArgs |> Seq.exists (fun (a, b) -> not (obj.ReferenceEquals(a, b)))
                    let objChanged = m.Object <> null && not (obj.ReferenceEquals(newObj, m.Object))
                    if argsChanged || objChanged then
                        if m.Object <> null then Expression.Call(newObj, m.Method, newArgs) :> Expression
                        else Expression.Call(m.Method, newArgs) :> Expression
                    else exp
            else
                // Not an Option.map pipe: generic MethodCall rewrite
                let newArgs = m.Arguments |> Seq.map rewrite |> Seq.toArray
                let newObj = if m.Object <> null then rewrite m.Object else null
                let argsChanged = Seq.zip m.Arguments newArgs |> Seq.exists (fun (a, b) -> not (obj.ReferenceEquals(a, b)))
                let objChanged = m.Object <> null && not (obj.ReferenceEquals(newObj, m.Object))
                if argsChanged || objChanged then
                    if m.Object <> null then Expression.Call(newObj, m.Method, newArgs) :> Expression
                    else Expression.Call(m.Method, newArgs) :> Expression
                else exp

        // --- Generic recursive cases ---
        | Lambda x ->
            // If any lambda parameter has a table type, pre-populate paramToAlias
            for p in x.Parameters do
                if isTableType p.Type then
                    match typeToAlias.TryGetValue(p.Type) with
                    | true, alias -> paramToAlias.[p] <- alias
                    | false, _ -> ()
            let newBody = rewrite x.Body
            if obj.ReferenceEquals(newBody, x.Body) then exp
            else Expression.Lambda(x.Type, newBody, x.Parameters) :> Expression

        | MethodCall m ->
            let newArgs = m.Arguments |> Seq.map rewrite |> Seq.toArray
            let newObj = if m.Object <> null then rewrite m.Object else null
            let argsChanged = Seq.zip m.Arguments newArgs |> Seq.exists (fun (a, b) -> not (obj.ReferenceEquals(a, b)))
            let objChanged = m.Object <> null && not (obj.ReferenceEquals(newObj, m.Object))
            if argsChanged || objChanged then
                if m.Object <> null then
                    Expression.Call(newObj, m.Method, newArgs) :> Expression
                else
                    Expression.Call(m.Method, newArgs) :> Expression
            else exp

        | :? InvocationExpression as inv ->
            // Propagate provenance: if this is Invoke(Lambda(params), args),
            // set provenance for lambda params from the invocation arguments.
            match inv.Expression with
            | :? LambdaExpression as lam ->
                for i in 0 .. min (lam.Parameters.Count - 1) (inv.Arguments.Count - 1) do
                    let param = lam.Parameters.[i]
                    match getProvenance inv.Arguments.[i] with
                    | Some alias -> paramToAlias.[param] <- alias
                    | None -> ()
            | _ -> ()
            let newExpr = rewrite inv.Expression
            let newArgs = inv.Arguments |> Seq.map rewrite |> Seq.toArray
            let exprChanged = not (obj.ReferenceEquals(newExpr, inv.Expression))
            let argsChanged = Seq.zip inv.Arguments newArgs |> Seq.exists (fun (a, b) -> not (obj.ReferenceEquals(a, b)))
            if exprChanged || argsChanged then
                Expression.Invoke(newExpr, newArgs) :> Expression
            else exp

        | New n ->
            let newArgs = n.Arguments |> Seq.map rewrite |> Seq.toArray
            Expression.New(n.Constructor, newArgs) :> Expression

        | :? NewArrayExpression as na when na.NodeType = ExpressionType.NewArrayInit ->
            let newExprs = na.Expressions |> Seq.map rewrite |> Seq.toArray
            Expression.NewArrayInit(na.Type.GetElementType(), newExprs) :> Expression

        | :? MemberInitExpression as mi ->
            let newExpr = rewrite (mi.NewExpression :> Expression) :?> NewExpression
            Expression.MemberInit(newExpr, mi.Bindings) :> Expression

        | :? ListInitExpression as li ->
            let newExpr = rewrite (li.NewExpression :> Expression) :?> NewExpression
            Expression.ListInit(newExpr, li.Initializers) :> Expression

        | :? BlockExpression as blk ->
            // Propagate provenance for block variables assigned from provenance-bearing expressions
            for expr in blk.Expressions do
                if expr.NodeType = ExpressionType.Assign then
                    let bin = expr :?> BinaryExpression
                    match bin.Left with
                    | Parameter p ->
                        match getProvenance bin.Right with
                        | Some alias -> paramToAlias.[p] <- alias
                        | None -> ()
                    | _ -> ()
            let newExprs = blk.Expressions |> Seq.map rewrite |> Seq.toArray
            Expression.Block(blk.Type, blk.Variables, newExprs) :> Expression

        | :? LoopExpression as lp ->
            let newBody = rewrite lp.Body
            if obj.ReferenceEquals(newBody, lp.Body) then exp
            else Expression.Loop(newBody, lp.BreakLabel, lp.ContinueLabel) :> Expression

        | :? TryExpression as tr ->
            let newBody = rewrite tr.Body
            let newFault = if tr.Fault <> null then rewrite tr.Fault else null
            let newFinally = if tr.Finally <> null then rewrite tr.Finally else null
            Expression.MakeTry(tr.Type, newBody, newFinally, newFault, tr.Handlers) :> Expression

        | :? SwitchExpression as sw ->
            let newVal = rewrite sw.SwitchValue
            let newDefault = if sw.DefaultBody <> null then rewrite sw.DefaultBody else null
            Expression.Switch(sw.Type, newVal, newDefault, sw.Comparison, sw.Cases) :> Expression

        // --- Unary (incl. Quote, TypeAs, Convert) ---
        | Unary u ->
            let newOperand = rewrite u.Operand
            if obj.ReferenceEquals(newOperand, u.Operand) then exp
            else Expression.MakeUnary(u.NodeType, newOperand, u.Type, u.Method) :> Expression

        | Binary b ->
            let newLeft = rewrite b.Left
            let newRight = rewrite b.Right
            if obj.ReferenceEquals(newLeft, b.Left) && obj.ReferenceEquals(newRight, b.Right) then exp
            else Expression.MakeBinary(b.NodeType, newLeft, newRight, b.IsLiftedToNull, b.Method) :> Expression

        | :? ConditionalExpression as c ->
            let newTest = rewrite c.Test
            let newIfTrue = rewrite c.IfTrue
            let newIfFalse = rewrite c.IfFalse
            if obj.ReferenceEquals(newTest, c.Test) && obj.ReferenceEquals(newIfTrue, c.IfTrue) && obj.ReferenceEquals(newIfFalse, c.IfFalse) then exp
            else Expression.Condition(newTest, newIfTrue, newIfFalse) :> Expression

        | :? TypeBinaryExpression as tb ->
            let newExpr = rewrite tb.Expression
            if obj.ReferenceEquals(newExpr, tb.Expression) then exp
            else
                if tb.NodeType = ExpressionType.TypeIs then Expression.TypeIs(newExpr, tb.TypeOperand) :> Expression
                else Expression.TypeEqual(newExpr, tb.TypeOperand) :> Expression

        // --- Leaf detection ---
        | Parameter p when isTableType p.Type ->
            let alias = resolveTableAlias p
            // Only create a TableLeaf if the analysis determined the full record is needed
            let needsFullRecord =
                match aliasUsage.TryGetValue(alias) with
                | true, usage -> usage.RequiresFullRecord
                | false, _ -> true // Default to full record if not analyzed
            if needsFullRecord then
                let key = $"__table:{alias}"
                getOrAddLeaf key
                    (fun idx -> TableLeaf (alias, p.Type, idx))
                    p.Type
            else
                // Column-only alias: individual columns will be handled by member access cases
                // Return the expression unchanged (it will be pruned by outer rewrite)
                exp

        | Parameter _ -> exp

        | Member m when m.Member.DeclaringType <> null && m.Member.DeclaringType |> isOptionOrNullableType ->
            let newExpr = rewrite m.Expression
            if obj.ReferenceEquals(newExpr, m.Expression) then exp
            else Expression.MakeMemberAccess(newExpr, m.Member) :> Expression

        | Member m when m.Expression <> null && isTableExpr m.Expression ->
            // Column access on a table parameter
            let alias = resolveAliasFromExpr m.Expression
            let tableKey = $"__table:{alias}"
            match leafKeys.TryGetValue(tableKey) with
            | true, tableIdx ->
                let tableAccess =
                    Expression.Convert(
                        Expression.ArrayIndex(paramArray, Expression.Constant(tableIdx)),
                        leaves.[tableIdx] |> function TableLeaf (_, t, _) -> t | _ -> failwith "expected TableLeaf") :> Expression
                let recordAccess =
                    let tableType = (leaves.[tableIdx] |> function TableLeaf (_, t, _) -> t | _ -> failwith "expected TableLeaf")
                    if isOptionType tableType then
                        let valueProp = tableType.GetProperty("Value")
                        Expression.MakeMemberAccess(tableAccess, valueProp) :> Expression
                    else
                        tableAccess
                Expression.MakeMemberAccess(recordAccess, m.Member) :> Expression
            | false, _ ->
                // Determine optionality: column is optional if it's already Option/Nullable,
                // or if the outer param it resolves to is Option<RecordType> (leftJoin)
                let isOptional =
                    m.Type.IsGenericType && m.Type.GetGenericTypeDefinition() = typedefof<Option<_>>
                let isNullable =
                    m.Type.IsGenericType && m.Type.GetGenericTypeDefinition() = typedefof<Nullable<_>>
                // Check if this column comes through an optional outer table
                let isOuterOptional =
                    match outerParamTypes.TryGetValue(alias) with
                    | true, outerType -> isOptionType outerType
                    | false, _ -> false
                let finalOptional = isOptional || isOuterOptional
                let colType =
                    if isOuterOptional && not isOptional && not isNullable then
                        // Wrap column type in Option since the outer table is optional
                        typedefof<Option<_>>.MakeGenericType(m.Type)
                    else
                        m.Type
                let key = $"{alias}.{m.Member.Name}"
                getOrAddLeaf key
                    (fun idx -> ColumnLeaf (alias, m.Member.Name, colType, finalOptional, isNullable, idx))
                    colType

        | Member m when m.Expression <> null ->
            let newExpr = rewrite m.Expression
            if obj.ReferenceEquals(newExpr, m.Expression) then exp
            else Expression.MakeMemberAccess(newExpr, m.Member) :> Expression

        | Constant _ -> exp

        | :? DefaultExpression -> exp

        | _ -> exp

    and rewriteSqlFunction (m: MethodCallExpression) (originalExp: Expression) =
        let qualifyCol alias (mem: MemberInfo) = $"[{alias}].[{mem.Name}]"
        /// Like visitSqlFn but uses provenance-aware alias resolution
        /// so that pattern variables (e.g., from match) resolve to
        /// the correct table alias.
        let rec visitSqlFnWithProvenance (exp: Expression) : string =
            match exp with
            | MethodCall m ->
                let fnName = m.Method.Name
                let args =
                    m.Arguments
                    |> Seq.map (fun arg ->
                        match arg with
                        | Member mem ->
                            let alias = resolveAliasFromExpr mem.Expression
                            qualifyCol alias mem.Member
                        | Constant c when c.Value = null ->
                            "NULL"
                        | Constant c when c.Type = typeof<string> ->
                            $"'{c.Value}'"
                        | Constant c ->
                            sprintf "%O" c.Value
                        | MethodCall _ as nested ->
                            visitSqlFnWithProvenance nested
                        | _ ->
                            notImplMsg $"Unsupported argument type in SQL function: {arg.NodeType}"
                    )
                    |> String.concat ", "
                $"{fnName}({args})"
            | _ ->
                notImplMsg $"Expected a method call expression but got: {exp.NodeType}"

        let sqlFragment = visitSqlFnWithProvenance (m :> Expression)
        let exprAlias = $"__hydra_expr_{!sqlExprCounter}"
        sqlExprCounter := !sqlExprCounter + 1
        let key = $"__sqlfn:{sqlFragment}"
        getOrAddLeaf key
            (fun idx -> SqlExprLeaf (sqlFragment, originalExp.Type, exprAlias, idx))
            originalExp.Type

    let actualBody = unwrapBodyWithParams (selectExpression :> Expression)

    // Run projection shape analysis before rewriting
    aliasUsage <- analyzeProjectionShape actualBody outerParamNames outerParamTypes typeToAlias

    let rewrittenBody = rewrite actualBody

    // Build the leaf tuple type and compiled mapper
    let leafTypes =
        leaves
        |> Seq.map (fun leaf ->
            match leaf with
            | TableLeaf (_, tableType, _) -> tableType
            | ColumnLeaf (_, _, colType, _, _, _) -> colType
            | SqlExprLeaf (_, resultType, _, _) -> resultType)
        |> Seq.toArray

    let leafTupleType =
        if leafTypes.Length = 0 then typeof<unit>
        elif leafTypes.Length = 1 then leafTypes.[0]
        else FSharp.Reflection.FSharpType.MakeTupleType(leafTypes)

    // Compile the mapper: obj[] -> obj
    let convertedBody =
        if rewrittenBody.Type.IsValueType then
            Expression.Convert(rewrittenBody, typeof<obj>) :> Expression
        else
            rewrittenBody
    let mapperLambda = Expression.Lambda<Func<obj[], obj>>(convertedBody, paramArray)
    let compiledMapper =
        try mapperLambda.CompileFast<Func<obj[], obj>>()
        with _ -> mapperLambda.Compile()

    {
        Leaves = leaves |> Seq.toList
        LeafTupleType = leafTupleType
        CompiledMapper = compiledMapper
    }
