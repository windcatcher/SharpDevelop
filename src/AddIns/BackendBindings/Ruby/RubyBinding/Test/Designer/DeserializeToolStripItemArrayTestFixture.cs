﻿// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Windows.Forms;
using ICSharpCode.RubyBinding;
using IronRuby.Compiler.Ast;
using NUnit.Framework;
using RubyBinding.Tests.Utils;

namespace RubyBinding.Tests.Designer
{
	[TestFixture]
	public class DeserializeToolStripItemArrayTestFixture : DeserializeAssignmentTestFixtureBase
	{		
		ToolStripMenuItem fileMenuItem;
		ToolStripMenuItem editMenuItem;
		
		public override string GetRubyCode()
		{
			fileMenuItem = (ToolStripMenuItem)componentCreator.CreateComponent(typeof(ToolStripMenuItem), "fileToolStripMenuItem");
			editMenuItem = (ToolStripMenuItem)componentCreator.CreateComponent(typeof(ToolStripMenuItem), "editToolStripMenuItem");
			
			componentCreator.Add(fileMenuItem, "fileToolStripMenuItem");
			componentCreator.Add(editMenuItem, "editToolStripMenuItem");
			
			return "self.Items = System::Array[System::Windows::Forms::ToolStripItem].new(\r\n" +
				"    [@fileToolStripMenuItem,\r\n" + 
				"    @editToolStripMenuItem])";
		}
		
		[Test]
		public void DeserializedObjectIsExpectedCustomColor()
		{
			ToolStripItem[] expectedArray = new ToolStripItem[] {fileMenuItem, editMenuItem};
			Assert.AreEqual(expectedArray, deserializedObject);
		}
		
		[Test]
		public void StringTypeResolved()
		{
			Assert.AreEqual("System.Windows.Forms.ToolStripItem", componentCreator.LastTypeNameResolved);
		}
	}
}
