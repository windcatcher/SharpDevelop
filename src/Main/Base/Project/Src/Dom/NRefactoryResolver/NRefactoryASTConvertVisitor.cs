// created on 04.08.2003 at 17:49
using System;
using System.Text;
using System.Drawing;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;

using RefParser = ICSharpCode.NRefactory.Parser;
using AST = ICSharpCode.NRefactory.Parser.AST;
using ICSharpCode.SharpDevelop.Dom;
using ICSharpCode.Core;

namespace ICSharpCode.SharpDevelop.Dom.NRefactoryResolver
{
	public class NRefactoryASTConvertVisitor : RefParser.AbstractASTVisitor
	{
		ICompilationUnit cu;
		Stack currentNamespace = new Stack();
		Stack<DefaultClass> currentClass = new Stack<DefaultClass>();
		
		public ICompilationUnit Cu {
			get {
				return cu;
			}
		}
		
		public NRefactoryASTConvertVisitor(IProjectContent projectContent)
		{
			cu = new DefaultCompilationUnit(projectContent);
		}
		
		DefaultClass GetCurrentClass()
		{
			return currentClass.Count == 0 ? null : currentClass.Peek();
		}
		
		ModifierEnum ConvertModifier(AST.Modifier m)
		{
			return ConvertModifier(m, ModifierEnum.Private);
		}
		
		ModifierEnum ConvertModifier(AST.Modifier m, ModifierEnum defaultModifier)
		{
			ModifierEnum r = (ModifierEnum)m;
			if (r == ModifierEnum.None)
				return defaultModifier;
			else
				return r;
		}
		
		public override object Visit(AST.CompilationUnit compilationUnit, object data)
		{
			//TODO: usings, Comments
			if (compilationUnit == null) {
				return null;
			}
			compilationUnit.AcceptChildren(this, data);
			return cu;
		}
		
		public override object Visit(AST.UsingDeclaration usingDeclaration, object data)
		{
			DefaultUsing us = new DefaultUsing(cu.ProjectContent, GetRegion(usingDeclaration.StartLocation, usingDeclaration.EndLocation));
			foreach (AST.Using u in usingDeclaration.Usings) {
				u.AcceptVisitor(this, us);
			}
			cu.Usings.Add(us);
			return data;
		}
		
		public override object Visit(AST.Using u, object data)
		{
			Debug.Assert(data is DefaultUsing);
			DefaultUsing us = (DefaultUsing)data;
			if (u.IsAlias) {
				us.Aliases[u.Name] = u.Alias;
			} else {
				us.Usings.Add(u.Name);
			}
			return data;
		}
		
		List<IAttributeSection> VisitAttributes(ArrayList attributes)
		{
			// TODO Expressions???
			List<IAttributeSection> result = new List<IAttributeSection>();
			foreach (AST.AttributeSection section in attributes) {
				List<IAttribute> resultAttributes = new List<IAttribute>();
				foreach (AST.Attribute attribute in section.Attributes) {
					IAttribute a = new DefaultAttribute(attribute.Name, new ArrayList(attribute.PositionalArguments), new SortedList());
					foreach (AST.NamedArgumentExpression n in attribute.NamedArguments) {
						a.NamedArguments[n.Name] = n.Expression;
					}
				}
				AttributeTarget target = AttributeTarget.None;
				if (section.AttributeTarget != null && section.AttributeTarget != "") {
					switch (section.AttributeTarget.ToUpper()) {
						case "ASSEMBLY":
							target = AttributeTarget.Assembly;
							break;
						case "FIELD":
							target = AttributeTarget.Field;
							break;
						case "EVENT":
							target = AttributeTarget.Event;
							break;
						case "METHOD":
							target = AttributeTarget.Method;
							break;
						case "MODULE":
							target = AttributeTarget.Module;
							break;
						case "PARAM":
							target = AttributeTarget.Param;
							break;
						case "PROPERTY":
							target = AttributeTarget.Property;
							break;
						case "RETURN":
							target = AttributeTarget.Return;
							break;
						case "TYPE":
							target = AttributeTarget.Type;
							break;
						default:
							target = AttributeTarget.None;
							break;
							
					}
				}
				IAttributeSection s = new DefaultAttributeSection(target, resultAttributes);
				result.Add(s);
			}
			return result;
		}
		
		public override object Visit(AST.NamespaceDeclaration namespaceDeclaration, object data)
		{
			string name;
			if (currentNamespace.Count == 0) {
				name = namespaceDeclaration.Name;
			} else {
				name = (string)currentNamespace.Peek() + '.' + namespaceDeclaration.Name;
			}
			
			currentNamespace.Push(name);
			object ret = namespaceDeclaration.AcceptChildren(this, data);
			currentNamespace.Pop();
			return ret;
		}
		
		ClassType TranslateClassType(AST.Types type)
		{
			switch (type) {
				case AST.Types.Class:
					return ClassType.Class;
				case AST.Types.Enum:
					return ClassType.Enum;
				case AST.Types.Interface:
					return ClassType.Interface;
				case AST.Types.Struct:
					return ClassType.Struct;
			}
			return ClassType.Class;
		}
		
		DefaultRegion GetRegion(Point start, Point end)
		{
			return new DefaultRegion(start.Y, start.X, end.Y, end.X);
		}
		
		public override object Visit(AST.TypeDeclaration typeDeclaration, object data)
		{
			DefaultRegion region = GetRegion(typeDeclaration.StartLocation, typeDeclaration.EndLocation);
			DefaultClass c = new DefaultClass(cu, TranslateClassType(typeDeclaration.Type), ConvertModifier(typeDeclaration.Modifier, ModifierEnum.Internal), region, GetCurrentClass());
			c.Attributes.AddRange(VisitAttributes(typeDeclaration.Attributes));
			
			if (currentClass.Count > 0) {
				DefaultClass cur = GetCurrentClass();
				cur.InnerClasses.Add(c);
				c.FullyQualifiedName = cur.FullyQualifiedName + '.' + typeDeclaration.Name;
			} else {
				if (currentNamespace.Count == 0) {
					c.FullyQualifiedName = typeDeclaration.Name;
				} else {
					c.FullyQualifiedName = (string)currentNamespace.Peek() + '.' + typeDeclaration.Name;
				}
				cu.Classes.Add(c);
			}
			if (typeDeclaration.BaseTypes != null) {
				foreach (string type in typeDeclaration.BaseTypes) {
					c.BaseTypes.Add(type);
				}
			}
			int index = 0;
			foreach (AST.TemplateDefinition template in typeDeclaration.Templates) {
				c.TypeParameters.Add(new DefaultTypeParameter(c, template.Name, index++));
			}
			currentClass.Push(c);
			object ret = typeDeclaration.AcceptChildren(this, data);
			currentClass.Pop();
			return ret;
		}
		
		public override object Visit(AST.DelegateDeclaration delegateDeclaration, object data)
		{
			DefaultRegion region = GetRegion(delegateDeclaration.StartLocation, delegateDeclaration.EndLocation);
			DefaultClass c = new DefaultClass(cu, ClassType.Delegate, ConvertModifier(delegateDeclaration.Modifier, ModifierEnum.Internal), region, GetCurrentClass());
			c.Attributes.AddRange(VisitAttributes(delegateDeclaration.Attributes));
			c.BaseTypes.Add("System.Delegate");
			if (currentClass.Count > 0) {
				DefaultClass cur = GetCurrentClass();
				cur.InnerClasses.Add(c);
				c.FullyQualifiedName = cur.FullyQualifiedName + '.' + delegateDeclaration.Name;
			} else {
				if (currentNamespace.Count == 0) {
					c.FullyQualifiedName = delegateDeclaration.Name;
				} else {
					c.FullyQualifiedName = (string)currentNamespace.Peek() + '.' + delegateDeclaration.Name;
				}
				cu.Classes.Add(c);
			}
			currentClass.Push(c); // necessary for CreateReturnType
			DefaultMethod invokeMethod = new DefaultMethod("Invoke", CreateReturnType(delegateDeclaration.ReturnType), ModifierEnum.Public, null, null, c);
			if (delegateDeclaration.Parameters != null) {
				foreach (AST.ParameterDeclarationExpression par in delegateDeclaration.Parameters) {
					IReturnType parType = CreateReturnType(par.TypeReference);
					invokeMethod.Parameters.Add(new DefaultParameter(par.ParameterName, parType, null));
				}
			}
			c.Methods.Add(invokeMethod);
			invokeMethod = new DefaultMethod("BeginInvoke", CreateReturnType(typeof(IAsyncResult)), ModifierEnum.Public, null, null, c);
			if (delegateDeclaration.Parameters != null) {
				foreach (AST.ParameterDeclarationExpression par in delegateDeclaration.Parameters) {
					IReturnType parType = CreateReturnType(par.TypeReference);
					invokeMethod.Parameters.Add(new DefaultParameter(par.ParameterName, parType, null));
				}
			}
			invokeMethod.Parameters.Add(new DefaultParameter("callback", CreateReturnType(typeof(AsyncCallback)), null));
			invokeMethod.Parameters.Add(new DefaultParameter("object", CreateReturnType(typeof(object)), null));
			c.Methods.Add(invokeMethod);
			invokeMethod = new DefaultMethod("EndInvoke", CreateReturnType(delegateDeclaration.ReturnType), ModifierEnum.Public, null, null, c);
			invokeMethod.Parameters.Add(new DefaultParameter("result", CreateReturnType(typeof(IAsyncResult)), null));
			c.Methods.Add(invokeMethod);
			currentClass.Pop();
			return c;
		}
		
		public override object Visit(AST.MethodDeclaration methodDeclaration, object data)
		{
			DefaultRegion region     = GetRegion(methodDeclaration.StartLocation, methodDeclaration.EndLocation);
			DefaultRegion bodyRegion = GetRegion(methodDeclaration.EndLocation, methodDeclaration.Body != null ? methodDeclaration.Body.EndLocation : new Point(-1, -1));
			IReturnType type = CreateReturnType(methodDeclaration.TypeReference);
			DefaultClass c  = GetCurrentClass();
			
			DefaultMethod method = new DefaultMethod(methodDeclaration.Name, type, ConvertModifier(methodDeclaration.Modifier), region, bodyRegion, GetCurrentClass());
			method.Attributes.AddRange(VisitAttributes(methodDeclaration.Attributes));
			if (methodDeclaration.Parameters != null) {
				foreach (AST.ParameterDeclarationExpression par in methodDeclaration.Parameters) {
					IReturnType parType = CreateReturnType(par.TypeReference);
					DefaultParameter p = new DefaultParameter(par.ParameterName, parType, new DefaultRegion(par.StartLocation, methodDeclaration.Body.EndLocation));
					method.Parameters.Add(p);
				}
			}
			c.Methods.Add(method);
			return null;
		}
		
		public override object Visit(AST.ConstructorDeclaration constructorDeclaration, object data)
		{
			DefaultRegion region     = GetRegion(constructorDeclaration.StartLocation, constructorDeclaration.EndLocation);
			DefaultRegion bodyRegion = GetRegion(constructorDeclaration.EndLocation, constructorDeclaration.Body != null ? constructorDeclaration.Body.EndLocation : new Point(-1, -1));
			DefaultClass c = GetCurrentClass();
			
			Constructor constructor = new Constructor(ConvertModifier(constructorDeclaration.Modifier), region, bodyRegion, GetCurrentClass());
			constructor.Attributes.AddRange(VisitAttributes(constructorDeclaration.Attributes));
			if (constructorDeclaration.Parameters != null) {
				foreach (AST.ParameterDeclarationExpression par in constructorDeclaration.Parameters) {
					IReturnType parType = CreateReturnType(par.TypeReference);
					DefaultParameter p = new DefaultParameter(par.ParameterName, parType, new DefaultRegion(par.StartLocation, constructorDeclaration.Body.EndLocation));
					constructor.Parameters.Add(p);
				}
			}
			c.Methods.Add(constructor);
			return null;
		}
		
		public override object Visit(AST.DestructorDeclaration destructorDeclaration, object data)
		{
			DefaultRegion region     = GetRegion(destructorDeclaration.StartLocation, destructorDeclaration.EndLocation);
			DefaultRegion bodyRegion = GetRegion(destructorDeclaration.EndLocation, destructorDeclaration.Body != null ? destructorDeclaration.Body.EndLocation : new Point(-1, -1));
			
			DefaultClass c = GetCurrentClass();
			
			Destructor destructor = new Destructor(region, bodyRegion, c);
			destructor.Attributes.AddRange(VisitAttributes(destructorDeclaration.Attributes));
			c.Methods.Add(destructor);
			return null;
		}
		
		
		public override object Visit(AST.FieldDeclaration fieldDeclaration, object data)
		{
			DefaultRegion region = GetRegion(fieldDeclaration.StartLocation, fieldDeclaration.EndLocation);
			DefaultClass c = GetCurrentClass();
			if (currentClass.Count > 0) {
				for (int i = 0; i < fieldDeclaration.Fields.Count; ++i) {
					AST.VariableDeclaration field = (AST.VariableDeclaration)fieldDeclaration.Fields[i];
					
					IReturnType retType;
					if (c.ClassType == ClassType.Enum)
						retType = c.DefaultReturnType;
					else
						retType = CreateReturnType(fieldDeclaration.GetTypeForField(i));
					DefaultField f = new DefaultField(retType, field.Name, ConvertModifier(fieldDeclaration.Modifier), region, c);
					f.Attributes.AddRange(VisitAttributes(fieldDeclaration.Attributes));
					if (c.ClassType == ClassType.Enum) {
						f.Modifiers = ModifierEnum.Const | ModifierEnum.Public;
					}
					
					c.Fields.Add(f);
				}
			}
			return null;
		}
		
		public override object Visit(AST.PropertyDeclaration propertyDeclaration, object data)
		{
			DefaultRegion region     = GetRegion(propertyDeclaration.StartLocation, propertyDeclaration.EndLocation);
			DefaultRegion bodyRegion = GetRegion(propertyDeclaration.BodyStart,     propertyDeclaration.BodyEnd);
			
			IReturnType type = CreateReturnType(propertyDeclaration.TypeReference);
			DefaultClass c = GetCurrentClass();
			
			DefaultProperty property = new DefaultProperty(propertyDeclaration.Name, type, ConvertModifier(propertyDeclaration.Modifier), region, bodyRegion, GetCurrentClass());
			property.Attributes.AddRange(VisitAttributes(propertyDeclaration.Attributes));
			c.Properties.Add(property);
			return null;
		}
		
		public override object Visit(AST.EventDeclaration eventDeclaration, object data)
		{
			DefaultRegion region     = GetRegion(eventDeclaration.StartLocation, eventDeclaration.EndLocation);
			DefaultRegion bodyRegion = GetRegion(eventDeclaration.BodyStart,     eventDeclaration.BodyEnd);
			IReturnType type = CreateReturnType(eventDeclaration.TypeReference);
			DefaultClass c = GetCurrentClass();
			DefaultEvent e = null;
			
			if (eventDeclaration.VariableDeclarators != null) {
				foreach (ICSharpCode.NRefactory.Parser.AST.VariableDeclaration varDecl in eventDeclaration.VariableDeclarators) {
					e = new DefaultEvent(varDecl.Name, type, ConvertModifier(eventDeclaration.Modifier), region, bodyRegion, GetCurrentClass());
					e.Attributes.AddRange(VisitAttributes(eventDeclaration.Attributes));
					c.Events.Add(e);
				}
			} else {
				e = new DefaultEvent(eventDeclaration.Name, type, ConvertModifier(eventDeclaration.Modifier), region, bodyRegion, GetCurrentClass());
				e.Attributes.AddRange(VisitAttributes(eventDeclaration.Attributes));
				c.Events.Add(e);
			}
			return null;
		}
		
		public override object Visit(AST.IndexerDeclaration indexerDeclaration, object data)
		{
			DefaultRegion region     = GetRegion(indexerDeclaration.StartLocation, indexerDeclaration.EndLocation);
			DefaultRegion bodyRegion = GetRegion(indexerDeclaration.BodyStart,     indexerDeclaration.BodyEnd);
			List<IParameter> parameters = new List<IParameter>();
			DefaultIndexer i = new DefaultIndexer(CreateReturnType(indexerDeclaration.TypeReference), parameters, ConvertModifier(indexerDeclaration.Modifier), region, bodyRegion, GetCurrentClass());
			i.Attributes.AddRange(VisitAttributes(indexerDeclaration.Attributes));
			if (indexerDeclaration.Parameters != null) {
				foreach (AST.ParameterDeclarationExpression par in indexerDeclaration.Parameters) {
					IReturnType parType = CreateReturnType(par.TypeReference);
					DefaultParameter p = new DefaultParameter(par.ParameterName, parType, new DefaultRegion(par.StartLocation, indexerDeclaration.EndLocation));
					parameters.Add(p);
				}
			}
			DefaultClass c = GetCurrentClass();
			c.Indexer.Add(i);
			return null;
		}
		
		IReturnType CreateReturnType(AST.TypeReference reference)
		{
			IClass c = GetCurrentClass();
			if (c == null) return null;
			return TypeVisitor.CreateReturnType(reference, c, c.Region.BeginLine + 1, 1);
		}
		
		IReturnType CreateReturnType(Type type)
		{
			return ReflectionReturnType.Create(ProjectContentRegistry.GetMscorlibContent(), type);
		}
	}
}
