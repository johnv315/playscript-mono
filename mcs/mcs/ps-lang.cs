// Copyright 2013 Zynga Inc.
//	
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//		
//      Unless required by applicable law or agreed to in writing, software
//      distributed under the License is distributed on an "AS IS" BASIS,
//      WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//      See the License for the specific language governing permissions and
//      limitations under the License.

using System;
using System.Collections.Generic;
using Mono.CSharp.JavaScript;
using Mono.CSharp.Cpp;

#if STATIC
using IKVM.Reflection;
using IKVM.Reflection.Emit;
#else
using System.Reflection;
using System.Reflection.Emit;
#endif

namespace Mono.CSharp
{
	//
	// Constants
	//

	public static class PsConsts 
	{
		//
		// The namespace used for the root package.
		//
		public const string PsRootNamespace = "_root";
	}

	//
	// Expressions
	//

	//
	// ActionScript: Object initializers implement standard JSON style object
	// initializer syntax in the form { ident : expr [ , ... ] } or { "literal" : expr [, ... ]}
	// Like the array initializer, type is inferred from assignment type, parameter type, or
	// field, var initializer type, or of no type can be inferred it is of type Dictionary<String,Object>.
	//
	public partial class AsObjectInitializer : Expression
	{
		List<Expression> elements;
		BlockVariableDeclaration variable;
		Assign assign;
		TypeSpec inferredObjType;

		public AsObjectInitializer (List<Expression> init, Location loc)
		{
			elements = init;
			this.loc = loc;
		}

		public AsObjectInitializer (int count, Location loc)
			: this (new List<Expression> (count), loc)
		{
		}

		public AsObjectInitializer (Location loc)
			: this (4, loc)
		{
		}

		#region Properties

		public int Count {
			get { return elements.Count; }
		}

		public List<Expression> Elements {
			get {
				return elements;
			}
		}

		public Expression this [int index] {
			get {
				return elements [index];
			}
		}

		public BlockVariableDeclaration VariableDeclaration {
			get {
				return variable;
			}
			set {
				variable = value;
			}
		}

		public Assign Assign {
			get {
				return assign;
			}
			set {
				assign = value;
			}
		}

		#endregion

		public void Add (Expression expr)
		{
			elements.Add (expr);
		}

		public override bool ContainsEmitWithAwait ()
		{
			throw new NotSupportedException ();
		}

		public override Expression CreateExpressionTree (ResolveContext ec)
		{
			throw new NotSupportedException ("ET");
		}

		protected override void CloneTo (CloneContext clonectx, Expression t)
		{
			var target = (AsObjectInitializer) t;

			target.elements = new List<Expression> (elements.Count);
			foreach (var element in elements)
				target.elements.Add (element.Clone (clonectx));
		}

		protected override Expression DoResolve (ResolveContext rc)
		{
//			BEN: This won't work because the returned type won't pass Mono's type checkers.
//			if (rc.Target == Target.JavaScript) {
//				this.type = rc.BuiltinTypes.Dynamic;
//				this.eclass = ExprClass.Value;
//				foreach (ElementInitializer elem in Elements)
//					elem.Resolve (rc);
//				return this;
//			}

			TypeExpression type;

			// If PlayScript extended syntax, attempt to do type inference for object initializer
			if (rc.PsExtended) {
				if (inferredObjType != null) {
					type = new TypeExpression (inferredObjType, Location);
				} else if (variable != null) {
					if (variable.TypeExpression is VarExpr) {
						type = new TypeExpression (rc.BuiltinTypes.Dynamic, Location);
					} else {
						type = new TypeExpression (variable.Variable.Type, variable.Variable.Location);
					}
				} else if (assign != null && assign.Target.Type != null) {
					type = new TypeExpression (assign.Target.Type, assign.Target.Location);
				} else {
					type = new TypeExpression (rc.BuiltinTypes.Dynamic, Location);
				}
			} else {
				// ActionScript - Always use dynamic "expando" object.
				type = new TypeExpression (rc.BuiltinTypes.Dynamic, Location);
			}

			return new NewInitialize (type, null, 
				new CollectionOrObjectInitializers(elements, Location), Location).Resolve (rc);
		}

		public Expression InferredResolveWithObjectType(ResolveContext rc, TypeSpec objType) 
		{
			if (objType.Name == "ExpandoObject")
				objType = rc.BuiltinTypes.Dynamic;
			inferredObjType = objType;
			return Resolve (rc);
		}

		public override void Emit (EmitContext ec)
		{
			throw new InternalErrorException ("Missing Resolve call");
		}

		public override void EmitJs (JsEmitContext jec)
		{
			jec.Buf.Write ("{", Location);

			bool first = true;
			foreach (ElementInitializer elem in Elements) {
				if (!first)
					jec.Buf.Write (", ");
				jec.Buf.Write ("\"", elem.Name, "\":");
				elem.Source.EmitJs (jec);
				first = false;
			}

			jec.Buf.Write ("}");
		}

		public override object Accept (StructuralVisitor visitor)
		{
			var ret = visitor.Visit (this);

			if (visitor.AutoVisit) {
				if (visitor.Skip) {
					visitor.Skip = false;
					return ret;
				}
				if (visitor.Continue && elements != null) {
					foreach (var elem in elements) {
						if (visitor.Continue)
							elem.Accept (visitor);
					}
				}
			}

			return ret;
		}
	}

	//
	// ActionScript: Array initializer expression is a standard expression
	// allowed anywhere an expression is valid.  The type is inferred from
	// assignment type, parameter type, or field/variable initializer type.
	// If no type is inferred, the type is Vector.<Object>.
	//
	public partial class AsArrayInitializer : ArrayInitializer
	{
		Assign assign;
		TypeSpec inferredArrayType;
		FullNamedExpression vectorType;

		public AsArrayInitializer (List<Expression> init, Location loc)
			: base(init, loc)
		{
		}

		public AsArrayInitializer (int count, Location loc)
			: this (new List<Expression> (count), loc)
		{
		}

		public AsArrayInitializer (Location loc)
			: this (4, loc)
		{
		}

		#region Properties

		public Assign Assign {
			get {
				return assign;
			}
			set {
				assign = value;
			}
		}

		public FullNamedExpression VectorType {
			get {
				return vectorType;
			}
			set {
				vectorType = value;
			}
		}

		#endregion

		protected override Expression DoResolve (ResolveContext rc)
		{
//			BEN: This won't work because the returned type won't pass Mono's type checkers.
//			if (rc.Target == Target.JavaScript) {
//				this.type = rc.Module.PredefinedTypes.AsArray.Resolve();
//				this.eclass = ExprClass.Value;
//				foreach (var elem in Elements)
//					elem.Resolve (rc);
//				return this;
//			}

			TypeExpression type;
			if (vectorType != null) { // For new <Type> [ initializer ] expressions..
				var elemTypeSpec = vectorType.ResolveAsType(rc);
				if (elemTypeSpec != null) {
					type = new TypeExpression(
						rc.Module.PredefinedTypes.AsVector.Resolve().MakeGenericType (rc, new [] { elemTypeSpec }), Location);
				} else {
					type = new TypeExpression (rc.Module.PredefinedTypes.AsArray.Resolve(), Location);
				}
			} else if (rc.PsExtended) {
				if (inferredArrayType != null) {  // Inferring parameter, variable, expression types
					type = new TypeExpression (inferredArrayType, Location);
				} else if (variable != null) {    
					if (variable.TypeExpression is VarExpr || variable.Variable.Type == rc.BuiltinTypes.Dynamic) {
						type = new TypeExpression (rc.Module.PredefinedTypes.AsArray.Resolve(), Location);
					} else {
						type = new TypeExpression (variable.Variable.Type, variable.Variable.Location);
					}
				} else if (assign != null && assign.Target.Type != null) {
					if (assign.Target.Type == rc.BuiltinTypes.Dynamic) {
						type = new TypeExpression (rc.Module.PredefinedTypes.AsArray.Resolve(), Location);
					} else {
						type = new TypeExpression (assign.Target.Type, assign.Target.Location);
					}
				} else {
					type = new TypeExpression (rc.Module.PredefinedTypes.AsArray.Resolve(), Location);
				}
			} else {
				type = new TypeExpression (rc.Module.PredefinedTypes.AsArray.Resolve(), Location);
			}

			TypeSpec typeSpec = type.ResolveAsType(rc.MemberContext);
			if (typeSpec.IsArray) {
				ArrayCreation arrayCreate = (ArrayCreation)new ArrayCreation (type, this).Resolve (rc);
				return arrayCreate;
			} else {
				var initElems = new List<Expression>();
				foreach (var e in elements) {
					initElems.Add (new CollectionElementInitializer(e));
				}
				return new NewInitialize (type, null, 
					new CollectionOrObjectInitializers(initElems, Location), Location).Resolve (rc);
			}
		}

		public Expression InferredResolveWithArrayType(ResolveContext rc, TypeSpec arrayType) 
		{
			inferredArrayType = arrayType;
			return Resolve (rc);
		}

		public override void EmitJs (JsEmitContext jec)
		{
			jec.Buf.Write ("[", Location);
			
			bool first = true;
			foreach (var elem in Elements) {
				if (!first)
					jec.Buf.Write (", ");
				elem.EmitJs (jec);
				first = false;
			}
			
			jec.Buf.Write ("]");
		}

		public override object Accept (StructuralVisitor visitor)
		{
			var ret = visitor.Visit (this);

			if (visitor.AutoVisit) {
				if (visitor.Skip) {
					visitor.Skip = false;
					return ret;
				}
				if (visitor.Continue && elements != null) {
					foreach (var elem in elements) {
						if (visitor.Continue)
							elem.Accept (visitor);
					}
				}
			}

			return ret;
		}
	}

	//
	// ActionScript: Implements the ActionScript delete expression.
	// This expression is used to implement the delete expression as
	// well as the delete statement.  Handles both the element access
	// form or the member access form.
	//
	public partial class AsDelete : ExpressionStatement {

		public Expression Expr;
		private Invocation removeExpr;
		
		public AsDelete (Expression expr, Location l)
		{
			this.Expr = expr;
			loc = l;
		}

		public override bool IsSideEffectFree {
			get {
				return removeExpr.IsSideEffectFree;
			}
		}

		public override bool ContainsEmitWithAwait ()
		{
			return removeExpr.ContainsEmitWithAwait ();
		}

		protected override Expression DoResolve (ResolveContext ec)
		{
			if (ec.Target == Target.JavaScript) {
				type = ec.BuiltinTypes.Dynamic;
				eclass = ExprClass.Value;
				return this;
			}

			if (Expr is ElementAccess) {

				var elem_access = Expr as ElementAccess;

				if (elem_access.Arguments.Count != 1) {
					ec.Report.Error (7021, loc, "delete statement must have only one index argument.");
					return null;
				}

				var expr = elem_access.Expr.Resolve (ec);
				if (expr.Type == null) {
					return null;
				}

				if (expr.Type.IsArray) {
					ec.Report.Error (7021, loc, "delete statement not allowed on arrays.");
					return null;
				}

				if (ec.Target == Target.JavaScript) {
					Expr = Expr.Resolve(ec);
					return this;
				}

				if (!expr.Type.IsAsDynamicClass && (expr.Type.BuiltinType != BuiltinTypeSpec.Type.Dynamic))
				{
					ec.Report.Error (7021, loc, "delete statement only allowed on dynamic types or dynamic classes");
					return null;
				}

				// cast expression to IDynamicClass and invoke __DeleteDynamicValue
				var dynClass = new Cast(new MemberAccess(new SimpleName("PlayScript", loc), "IDynamicClass", loc), expr, loc);
				removeExpr = new Invocation (new MemberAccess (dynClass, "__DeleteDynamicValue", loc), elem_access.Arguments);
				return removeExpr.Resolve (ec);

			} else if (Expr is MemberAccess) {

				if (ec.Target == Target.JavaScript) {
					Expr = Expr.Resolve(ec);
					return this;
				}

				var memb_access = Expr as MemberAccess;

				var expr = memb_access.LeftExpression.Resolve (ec);
				if (expr.Type == null) {
					return null;
				}

				if (!expr.Type.IsAsDynamicClass && (expr.Type.BuiltinType != BuiltinTypeSpec.Type.Dynamic))
				{
					ec.Report.Error (7021, loc, "delete statement only allowed on dynamic types or dynamic classes");
					return null;
				}

				// cast expression to IDynamicClass and invoke __DeleteDynamicValue
				var dynClass = new Cast(new MemberAccess(new SimpleName("PlayScript", loc), "IDynamicClass", loc), expr, loc);
				var args = new Arguments(1);
				args.Add (new Argument(new StringLiteral(ec.BuiltinTypes, memb_access.Name, loc)));
				removeExpr = new Invocation (new MemberAccess (dynClass, "__DeleteDynamicValue", loc), args);
				return removeExpr.Resolve (ec);

			} else {
				// Error is reported elsewhere.
				return null;
			}
		}

		protected override void CloneTo (CloneContext clonectx, Expression t)
		{
			var target = (AsDelete) t;

			target.Expr = Expr.Clone (clonectx);
		}

		public override void Emit (EmitContext ec)
		{
			throw new System.NotImplementedException ();
		}

		public override void EmitStatement (EmitContext ec)
		{
			throw new System.NotImplementedException ();
		}

		public override void EmitJs (JsEmitContext jec)
		{
			jec.Buf.Write ("delete ", Location);
			Expr.EmitJs (jec);
		}

		public override void EmitStatementJs (JsEmitContext jec)
		{
			jec.Buf.Write ("\t", Location);
			EmitJs (jec);
			jec.Buf.Write (";\n");
		}

		public override Expression CreateExpressionTree (ResolveContext ec)
		{
			return removeExpr.CreateExpressionTree(ec);
		}

		public override object Accept (StructuralVisitor visitor)
		{
			var ret = visitor.Visit (this);

			if (visitor.AutoVisit) {
				if (visitor.Skip) {
					visitor.Skip = false;
					return ret;
				}
				if (visitor.Continue)
					this.Expr.Accept (visitor);
			}

			return ret;
		}
	}

	//
	// ActionScript: Implements the ActionScript new expression.
	// This expression is used to implement the as new expression 
	// which takes either a type expression, an AsArrayInitializer,
	// or an invocation expression of some form.
	//
	public partial class AsNew : ExpressionStatement {
		
		public Expression Expr;
		private Expression newExpr;

		public AsNew (Expression expr, Location l)
		{
			this.Expr = expr;
			loc = l;
		}
		
		public override bool IsSideEffectFree {
			get {
				return newExpr.IsSideEffectFree;
			}
		}
		
		public override bool ContainsEmitWithAwait ()
		{
			return newExpr.ContainsEmitWithAwait ();
		}
		
		protected override Expression DoResolve (ResolveContext ec)
		{
			if (ec.Target == Target.JavaScript) {
				type = ec.BuiltinTypes.Dynamic;
				eclass = ExprClass.Value;
				return this;
			}

			if (Expr is AsArrayInitializer)
				return Expr.Resolve (ec);

			New newExpr = null;

			if (Expr is Invocation) {
				var inv = Expr as Invocation;
				newExpr = new New(inv.Exp, inv.Arguments, loc);
			} else if (Expr is ElementAccess) {
				if (loc.SourceFile != null && !loc.SourceFile.PsExtended) {
					ec.Report.Error (7103, loc, "Native arrays are only suppored in ASX.'");
					return null;
				}
				var elemAcc = Expr as ElementAccess;
				var exprList = new List<Expression>();
				foreach (var arg in elemAcc.Arguments) {
					exprList.Add (arg.Expr);
				}
				// TODO: Handle jagged arrays
				var arrayCreate = new ArrayCreation ((FullNamedExpression) elemAcc.Expr, exprList, 
				                new ComposedTypeSpecifier (exprList.Count, loc), null, loc);
				return arrayCreate.Resolve (ec);
			} else {
				var resolveExpr = Expr.Resolve (ec);
				if (resolveExpr == null)
					return null;
				if (resolveExpr is TypeOf) {
					newExpr = new New (((TypeOf)resolveExpr).TypeExpression, new Arguments (0), loc);
				} else {
					newExpr = new New (resolveExpr, new Arguments (0), loc);
				}
			}

			return newExpr.Resolve (ec);
		}
		
		protected override void CloneTo (CloneContext clonectx, Expression t)
		{
			var target = (AsDelete) t;
			
			target.Expr = Expr.Clone (clonectx);
		}
		
		public override void Emit (EmitContext ec)
		{
			throw new System.NotImplementedException ();
		}
		
		public override void EmitStatement (EmitContext ec)
		{
			throw new System.NotImplementedException ();
		}
		
		public override void EmitJs (JsEmitContext jec)
		{
			jec.Buf.Write ("new ", Location);
			Expr.EmitJs (jec);
		}
		
		public override void EmitStatementJs (JsEmitContext jec)
		{
			jec.Buf.Write ("\t", Location);
			EmitJs (jec);
			jec.Buf.Write (";\n");
		}
		
		public override Expression CreateExpressionTree (ResolveContext ec)
		{
			return newExpr.CreateExpressionTree(ec);
		}
		
		public override object Accept (StructuralVisitor visitor)
		{
			var ret = visitor.Visit (this);

			if (visitor.AutoVisit) {
				if (visitor.Skip) {
					visitor.Skip = false;
					return ret;
				}
				if (visitor.Continue)
					this.Expr.Accept (visitor);
				if (visitor.Continue)
					this.newExpr.Accept (visitor);
			}

			return ret;
		}
	}

	//
	// ActionScript: Implements the ActionScript typeof expression.
	// This expression is for backwards compatibility with javascript
	// and is not supported in ASX.
	//
	public partial class AsTypeOf : ExpressionStatement {
		
		public Expression Expr;
		
		public AsTypeOf (Expression expr, Location l)
		{
			this.Expr = expr;
			loc = l;
		}
		
		public override bool IsSideEffectFree {
			get {
				return Expr.IsSideEffectFree;
			}
		}
		
		public override bool ContainsEmitWithAwait ()
		{
			return Expr.ContainsEmitWithAwait ();
		}
		
		protected override Expression DoResolve (ResolveContext ec)
		{
			if (ec.Target == Target.JavaScript) {
				type = ec.BuiltinTypes.Dynamic;
				eclass = ExprClass.Value;
				return this;
			}

			if (loc.SourceFile != null && loc.SourceFile.PsExtended) {
				ec.Report.Error (7101, loc, "'typeof' operator not supported in ASX.'");
				return null;
			}

			var args = new Arguments(1);
			args.Add (new Argument(Expr));

			return new Invocation(new MemberAccess(new MemberAccess(
				new SimpleName(PsConsts.PsRootNamespace, loc), "_typeof_fn", loc), "_typeof", loc), args).Resolve (ec);
		}
		
		protected override void CloneTo (CloneContext clonectx, Expression t)
		{
			var target = (AsDelete) t;
			
			target.Expr = Expr.Clone (clonectx);
		}
		
		public override void Emit (EmitContext ec)
		{
			throw new System.NotImplementedException ();
		}
		
		public override void EmitStatement (EmitContext ec)
		{
			throw new System.NotImplementedException ();
		}
		
		public override void EmitJs (JsEmitContext jec)
		{
			jec.Buf.Write ("new ", Location);
			Expr.EmitJs (jec);
		}
		
		public override void EmitStatementJs (JsEmitContext jec)
		{
			jec.Buf.Write ("\t", Location);
			EmitJs (jec);
			jec.Buf.Write (";\n");
		}
		
		public override Expression CreateExpressionTree (ResolveContext ec)
		{
			return Expr.CreateExpressionTree(ec);
		}
		
		public override object Accept (StructuralVisitor visitor)
		{
			var ret = visitor.Visit (this);

			if (visitor.AutoVisit) {
				if (visitor.Skip) {
					visitor.Skip = false;
					return ret;
				}
				if (visitor.Continue)
					this.Expr.Accept (visitor);
			}

			return ret;
		}
	}


	public partial class RegexLiteral : Constant, ILiteralConstant
	{
		readonly public string Regex;
		readonly public string Options;

		public RegexLiteral (BuiltinTypes types, string regex, string options, Location loc)
			: base (loc)
		{
			Regex = regex;
			Options = options ?? "";
		}

		public override bool IsLiteral {
			get { return true; }
		}

		public override object GetValue ()
		{
			return "/" + Regex + "/" + Options;
		}
		
		public override string GetValueAsLiteral ()
		{
			return GetValue () as String;
		}
		
		public override long GetValueAsLong ()
		{
			throw new NotSupportedException ();
		}

		public override void EncodeAttributeValue (IMemberContext rc, AttributeEncoder enc, TypeSpec targetType)
		{
			throw new NotSupportedException ();
		}
		
		public override bool IsDefaultValue {
			get {
				return Regex == null && Options == "";
			}
		}
		
		public override bool IsNegative {
			get {
				return false;
			}
		}
		
		public override bool IsNull {
			get {
				return IsDefaultValue;
			}
		}
		
		public override Constant ConvertExplicitly (bool in_checked_context, TypeSpec target_type, ResolveContext opt_ec)
		{
			return null;
		}

		protected override Expression DoResolve (ResolveContext rc)
		{
			if (rc.Target == Target.JavaScript) {
				type = rc.Module.PredefinedTypes.AsRegExp.Resolve();
				eclass = ExprClass.Value;
				return this;
			}

			var args = new Arguments(2);
			args.Add (new Argument(new StringLiteral(rc.BuiltinTypes, Regex, this.Location)));
			args.Add (new Argument(new StringLiteral(rc.BuiltinTypes, Options, this.Location)));

			return new New(new TypeExpression(rc.Module.PredefinedTypes.AsRegExp.Resolve(), this.Location), 
			               args, this.Location).Resolve (rc);
		}

		public override void Emit (EmitContext ec)
		{
			throw new NotSupportedException ();
		}

		public override void EmitJs (JsEmitContext jec)
		{
			jec.Buf.Write (GetValue () as String, Location);
		}

#if FULL_AST
		public char[] ParsedValue { get; set; }
#endif

		public override object Accept (StructuralVisitor visitor)
		{
			return visitor.Visit (this);
		}
	}

	public partial class XmlLiteral : Constant, ILiteralConstant
	{
		readonly public string Xml;

		public XmlLiteral (BuiltinTypes types, string xml, Location loc)
			: base (loc)
		{
			Xml = xml;
		}
		
		public override bool IsLiteral {
			get { return true; }
		}
		
		public override object GetValue ()
		{
			return Xml;
		}
		
		public override string GetValueAsLiteral ()
		{
			return GetValue () as String;
		}
		
		public override long GetValueAsLong ()
		{
			throw new NotSupportedException ();
		}
		
		public override void EncodeAttributeValue (IMemberContext rc, AttributeEncoder enc, TypeSpec targetType)
		{
			throw new NotSupportedException ();
		}
		
		public override bool IsDefaultValue {
			get {
				return Xml == null;
			}
		}
		
		public override bool IsNegative {
			get {
				return false;
			}
		}
		
		public override bool IsNull {
			get {
				return IsDefaultValue;
			}
		}
		
		public override Constant ConvertExplicitly (bool in_checked_context, TypeSpec target_type, ResolveContext opt_ec)
		{
			return null;
		}
		
		protected override Expression DoResolve (ResolveContext rc)
		{
			var args = new Arguments(1);
			args.Add (new Argument(new StringLiteral(rc.BuiltinTypes, Xml, this.Location)));

			return new New(new TypeExpression(rc.Module.PredefinedTypes.AsXml.Resolve(), this.Location), 
			               args, this.Location).Resolve (rc);
		}
		
		public override void Emit (EmitContext ec)
		{
			throw new NotSupportedException ();
		}
		
		public override void EmitJs (JsEmitContext jec)
		{
			jec.Buf.Write (GetValue () as String, Location);
		}
		
#if FULL_AST
		public char[] ParsedValue { get; set; }
#endif
		
		public override object Accept (StructuralVisitor visitor)
		{
			return visitor.Visit (this);
		}
	}

	/// <summary>
	///   Implementation of the ActionScript `in' operator.
	/// </summary>
	public partial class AsIn : Expression
	{
		protected Expression expr;
		protected Expression objExpr;

		public AsIn (Expression expr, Expression obj_expr, Location l)
		{
			this.expr = expr;
			this.objExpr = obj_expr;
			loc = l;
		}

		public Expression Expr {
			get {
				return expr;
			}
		}

		public Expression ObjectExpression {
			get {
				return objExpr;
			}
		}

		public override bool ContainsEmitWithAwait ()
		{
			throw new NotSupportedException ();
		}

		public override Expression CreateExpressionTree (ResolveContext ec)
		{
			throw new NotSupportedException ("ET");
		}

		protected override Expression DoResolve (ResolveContext ec)
		{
			if (ec.Target == Target.JavaScript) {
				expr = Expr.Resolve (ec);
				objExpr = objExpr.Resolve (ec);
				type = ec.BuiltinTypes.Bool;
				eclass = ExprClass.Value;
				return this;
			}

			var objExpRes = objExpr.Resolve (ec);

			var args = new Arguments (1);
			args.Add (new Argument (expr));

			if (objExpRes.Type == ec.BuiltinTypes.Dynamic) {
				// If dynamic, cast to IDictionary<string,object> and call ContainsKey
				var dictExpr = new TypeExpression(ec.Module.PredefinedTypes.IDictionaryGeneric.Resolve().MakeGenericType(ec, 
				                      new [] { ec.BuiltinTypes.String, ec.BuiltinTypes.Object }), loc);
				return new Invocation (new MemberAccess (new Cast(dictExpr, objExpr, loc), "ContainsKey", loc), args).Resolve (ec);
			} else {
				string containsMethodName = "Contains";
	
				if (objExpRes.Type != null && objExpRes.Type.ImplementsInterface (ec.Module.PredefinedTypes.IDictionary.Resolve(), true)) {
					containsMethodName = "ContainsKey";
				}

				return new Invocation (new MemberAccess (objExpr, containsMethodName, loc), args).Resolve (ec);
			}
		}

		protected override void CloneTo (CloneContext clonectx, Expression t)
		{
			AsIn target = (AsIn) t;

			target.expr = expr.Clone (clonectx);
			target.objExpr = objExpr.Clone (clonectx);
		}

		public override void Emit (EmitContext ec)
		{
			throw new InternalErrorException ("Missing Resolve call");
		}

		public override void EmitJs (JsEmitContext jec)
		{
			Expr.EmitJs (jec);
			jec.Buf.Write (" in ");
			ObjectExpression.EmitJs (jec);
		}

		public override object Accept (StructuralVisitor visitor)
		{
			var ret = visitor.Visit (this);

			if (visitor.AutoVisit) {
				if (visitor.Skip) {
					visitor.Skip = false;
					return ret;
				}
				if (visitor.Continue)
					this.expr.Accept (visitor);
				if (visitor.Continue)
					this.objExpr.Accept (visitor);
			}

			return ret;
		}

	}

	/// <summary>
	///   Implementation of the ActionScript `undefined' object constant.
	/// </summary>
	public partial class AsUndefinedLiteral : Expression
	{
		public AsUndefinedLiteral (Location l)
		{
			loc = l;
		}

		public override string ToString ()
		{
			return this.GetType ().Name + " (undefined)";
		}

		public override bool ContainsEmitWithAwait ()
		{
			throw new NotSupportedException ();
		}

		public override Expression CreateExpressionTree (ResolveContext ec)
		{
			throw new NotSupportedException ("ET");
		}

		protected override Expression DoResolve (ResolveContext ec)
		{
			if (ec.Target == Target.JavaScript) {
				type = ec.BuiltinTypes.Dynamic;
				eclass = ExprClass.Value;
				return this;
			}

			return new MemberAccess(new TypeExpression(ec.Module.PredefinedTypes.AsUndefined.Resolve(), loc), 
			                        "_undefined", loc).Resolve (ec);
		}

		protected override void CloneTo (CloneContext clonectx, Expression t)
		{
		}

		public override void Emit (EmitContext ec)
		{
			throw new InternalErrorException ("Missing Resolve call");
		}

		public override void EmitJs (JsEmitContext jec)
		{
			jec.Buf.Write ("undefined", Location);
		}

		public override object Accept (StructuralVisitor visitor)
		{
			return visitor.Visit (this);
		}

	}

	//
	// ActionScript: Implements the ActionScript delete expression.
	// This expression is used to implement the delete expression as
	// well as the delete statement.  Handles both the element access
	// form or the member access form.
	//
	public partial class AsLocalFunction : Statement {
		
		public string Name;
		public AnonymousMethodExpression MethodExpr;
		public BlockVariableDeclaration VarDecl;

		public AsLocalFunction (Location loc, string name, AnonymousMethodExpression methodExpr, BlockVariableDeclaration varDecl)
		{
			this.loc = loc;
			this.Name = name;
			this.MethodExpr = methodExpr;
			this.VarDecl = varDecl;
		}

		public override bool Resolve (BlockContext bc)
		{
			return true;
		}

		protected override void CloneTo (CloneContext clonectx, Statement t)
		{
			var target = (AsLocalFunction) t;

			target.Name = Name;
			target.MethodExpr = MethodExpr.Clone (clonectx) as AnonymousMethodExpression;
			target.VarDecl = VarDecl.Clone (clonectx) as BlockVariableDeclaration;
		}

		protected override void DoEmit (EmitContext ec)
		{
		}

//		public override void EmitJs (JsEmitContext jec)
//		{
//			jec.Buf.Write ("delete ", Location);
//			Expr.EmitJs (jec);
//		}
//		
//		public override void EmitStatementJs (JsEmitContext jec)
//		{
//			jec.Buf.Write ("\t", Location);
//			EmitJs (jec);
//			jec.Buf.Write (";\n");
//		}
		
		public override Expression CreateExpressionTree (ResolveContext ec)
		{
			throw new System.NotSupportedException ();
		}
		
		public override object Accept (StructuralVisitor visitor)
		{
			var ret = visitor.Visit (this);

			if (visitor.AutoVisit) {
				if (visitor.Skip) {
					visitor.Skip = false;
					return ret;
				}
				if (visitor.Continue)
					this.MethodExpr.Accept (visitor);
			}

			return ret;
		}
	}

	// Use namespace statement
	public partial class AsUseNamespaceStatement : Statement {

		public string NS;

		public AsUseNamespaceStatement(string ns, Location loc)
		{
			this.loc = loc;
			NS = ns;
		}

		public override bool Resolve (BlockContext ec)
		{
			return true;
		}
		
		public override bool ResolveUnreachable (BlockContext ec, bool warn)
		{
			return true;
		}
		
		public override void Emit (EmitContext ec)
		{
		}
		
		public override void EmitJs (JsEmitContext jec)
		{
		}

		protected override void DoEmit (EmitContext ec)
		{
			throw new NotSupportedException ();
		}
		
		protected override void CloneTo (CloneContext clonectx, Statement target)
		{
			// nothing needed.
		}
		
		public override object Accept (StructuralVisitor visitor)
		{
			return visitor.Visit (this);
		}

	}

	public partial class AsNonAssignStatementExpression : Statement
	{
		public Expression expr;
		
		public AsNonAssignStatementExpression (Expression expr)
		{
			this.expr = expr;
		}
		
		public Expression Expr {
			get {
				return expr;
			}
		}

		public override bool Resolve (BlockContext bc)
		{
			if (!base.Resolve (bc))
				return false;

			expr = expr.Resolve (bc);

			return expr != null;
		}

		protected override void DoEmit (EmitContext ec)
		{
			if (!expr.IsSideEffectFree) {
				expr.EmitSideEffect (ec);
			}
		}

		protected override void DoEmitJs (JsEmitContext jec) 
		{
			expr.EmitJs (jec);
		}
		
		public override void EmitJs (JsEmitContext jec)
		{
			DoEmitJs (jec);
		}

		protected override void CloneTo (CloneContext clonectx, Statement target)
		{
			var t = target as AsNonAssignStatementExpression;
			t.expr = expr.Clone (clonectx);
		}
		
		public override object Accept (StructuralVisitor visitor)
		{
			var ret = visitor.Visit (this);

			if (visitor.AutoVisit) {
				if (visitor.Skip) {
					visitor.Skip = false;
					return ret;
				}
				if (visitor.Continue)
					this.expr.Accept (visitor);
			}

			return ret;
		}
	}

	/// <summary>
	///   Implementation of the ActionScript E4X xml query.
	/// </summary>
	public partial class AsXmlQueryExpression : Expression
	{
		protected Expression expr;
		protected Expression query;
		
		public AsXmlQueryExpression (Expression expr, Expression query, Location l)
		{
			this.expr = expr;
			this.query = query;
			loc = l;
		}
		
		public Expression Expr {
			get {
				return expr;
			}
		}

		public Expression Query {
			get {
				return query;
			}
		}

		public override bool ContainsEmitWithAwait ()
		{
			throw new NotSupportedException ();
		}
		
		public override Expression CreateExpressionTree (ResolveContext ec)
		{
			throw new NotSupportedException ("ET");
		}
		
		protected override Expression DoResolve (ResolveContext ec)
		{
			// TODO: Implement XML query expression.
			return null;
		}
		
		protected override void CloneTo (CloneContext clonectx, Expression t)
		{
			AsXmlQueryExpression target = (AsXmlQueryExpression) t;
			
			target.expr = expr.Clone (clonectx);
			target.query = query.Clone (clonectx);
		}
		
		public override void Emit (EmitContext ec)
		{
			throw new InternalErrorException ("Missing Resolve call");
		}

		public override object Accept (StructuralVisitor visitor)
		{
			var ret = visitor.Visit (this);

			if (visitor.AutoVisit) {
				if (visitor.Skip) {
					visitor.Skip = false;
					return ret;
				}
				if (visitor.Continue)
					this.expr.Accept (visitor);
				if (visitor.Continue)
					this.query.Accept (visitor);
			}

			return ret;
		}
		
	}


}
