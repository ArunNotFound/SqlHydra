/// Normalizes LINQ expression trees before visitor processing.
/// Newer FSharp.Core versions (10.1+) emit different expression tree shapes:
/// - Lambda bodies wrapped in BlockExpressions with local variables
/// - Comparison operators as MethodCall expressions instead of BinaryExpression nodes
/// This module transforms those into predictable shapes so visitors don't need per-version handling.
module internal SqlHydra.Query.ExpressionNormalizer

open System.Linq.Expressions

/// Substitutes ParameterExpression references with their assigned values.
type private VariableInliner(substitutions: System.Collections.Generic.Dictionary<ParameterExpression, Expression>) =
    inherit ExpressionVisitor()
    override this.VisitParameter(node) =
        match substitutions.TryGetValue(node) with
        | true, value -> this.Visit(value)
        | _ -> node :> Expression

/// Maps comparison operator method names to their corresponding ExpressionType.
let private tryGetComparisonExpressionType (methodName: string) =
    match methodName with
    | "op_Equality" | "GenericEqualityIntrinsic" -> Some ExpressionType.Equal
    | "op_Inequality" | "GenericInequalityIntrinsic" -> Some ExpressionType.NotEqual
    | "op_GreaterThan" | "GenericGreaterThanIntrinsic" -> Some ExpressionType.GreaterThan
    | "op_GreaterThanOrEqual" | "GenericGreaterOrEqualIntrinsic" -> Some ExpressionType.GreaterThanOrEqual
    | "op_LessThan" | "GenericLessThanIntrinsic" -> Some ExpressionType.LessThan
    | "op_LessThanOrEqual" | "GenericLessOrEqualIntrinsic" -> Some ExpressionType.LessThanOrEqual
    | _ -> None

/// Recursively normalizes expression trees:
/// 1. Inlines BlockExpression variables (preserving tuple deconstructions)
/// 2. Converts comparison operator MethodCalls to BinaryExpression nodes
type private Normalizer() =
    inherit ExpressionVisitor()

    override this.VisitBlock(node) =
        // Build substitution map for non-tuple-deconstruction variables.
        // Variables assigned from MemberAccess (e.g., o = tupledArg.Item1) are preserved
        // because visitAlias extracts table aliases from ParameterExpression names.
        let substitutions = System.Collections.Generic.Dictionary<ParameterExpression, Expression>()
        for expr in node.Expressions do
            if expr.NodeType = ExpressionType.Assign then
                let bin = expr :?> BinaryExpression
                match bin.Left with
                | :? ParameterExpression as p when bin.Right.NodeType <> ExpressionType.MemberAccess ->
                    substitutions.[p] <- bin.Right
                | _ -> ()
        let result = node.Expressions |> Seq.last
        let inlined =
            if substitutions.Count > 0 then
                VariableInliner(substitutions).Visit(result)
            else
                result
        // Continue normalizing the inlined result (handles nested blocks, operator calls, etc.)
        this.Visit(inlined)

    override this.VisitMethodCall(node) =
        match tryGetComparisonExpressionType node.Method.Name with
        | Some exprType when node.Arguments.Count = 2 ->
            let left = this.Visit(node.Arguments.[0])
            let right = this.Visit(node.Arguments.[1])
            Expression.MakeBinary(exprType, left, right) :> Expression
        | _ ->
            base.VisitMethodCall(node)

/// Normalizes a LINQ expression tree into a predictable shape for visitor processing.
let normalize (expr: Expression) : Expression =
    Normalizer().Visit(expr)
