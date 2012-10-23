﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TextAdventures.Quest.Scripts;

namespace TextAdventures.Quest
{
    public class EditableScriptData
    {
        public EditableScriptData(Element editor)
        {
            DisplayString = editor.Fields.GetString("display");
            Category = editor.Fields.GetString("category");
            CreateString = editor.Fields.GetString("create");
            AdderDisplayString = editor.Fields.GetString("add");
            IsVisibleInSimpleMode = !editor.Fields.GetAsType<bool>("advanced");
            IsDesktopOnly = editor.Fields.GetAsType<bool>("desktop");
        }

        public string DisplayString { get; private set; }
        public string Category { get; private set; }
        public string CreateString { get; private set; }
        public string AdderDisplayString { get; private set; }
        public bool IsVisibleInSimpleMode { get; private set; }
        public bool IsDesktopOnly { get; private set; }
    }

    internal class EditableScriptFactory
    {
        private EditorController m_controller;
        private ScriptFactory m_scriptFactory;
        private WorldModel m_worldModel;
        private Dictionary<string, EditableScriptData> m_scriptData = new Dictionary<string, EditableScriptData>();
        private Dictionary<IScript, EditableScriptBase> m_cache = new Dictionary<IScript, EditableScriptBase>();

        internal EditableScriptFactory(EditorController controller, ScriptFactory factory, WorldModel worldModel)
        {
            m_controller = controller;
            m_scriptFactory = factory;
            m_worldModel = worldModel;

            foreach (Element editor in worldModel.Elements.GetElements(ElementType.Editor).Where(e => IsScriptEditor(e)))
            {
                string appliesTo = editor.Fields.GetString("appliesto");
                m_scriptData.Add(appliesTo, new EditableScriptData(editor));
            }
        }

        private bool IsScriptEditor(Element editor)
        {
            return !string.IsNullOrEmpty(editor.Fields.GetString("category"));
        }

        internal EditableScriptBase CreateEditableScript(string keyword)
        {
            IScript script = m_scriptFactory.CreateSimpleScript(keyword);
            return CreateEditableScript(script);
        }

        internal EditableScriptBase CreateEditableScript(IScript script)
        {
            EditableScriptBase newScript;

            if (m_cache.TryGetValue(script, out newScript))
            {
                return newScript;
            }

            IfScript ifScript = script as IfScript;
            if (ifScript != null)
            {
                newScript = new EditableIfScript(m_controller, ifScript, m_worldModel.UndoLogger);
            }
            else
            {
                EditableScript newEditableScript = new EditableScript(m_controller, script, m_worldModel.UndoLogger);
                if (script != null && m_scriptData.ContainsKey(script.Keyword)) newEditableScript.DisplayTemplate = m_scriptData[script.Keyword].DisplayString;
                newScript = newEditableScript;
            }
            m_cache.Add(script, newScript);
            return newScript;
        }

        internal EditableScriptBase CreateEditableFunctionCallScript()
        {
            IScript script = m_scriptFactory.CreateBlankFunctionCallScript();
            return CreateEditableScript(script);
        }

        internal IEnumerable<string> GetCategories(bool simpleModeOnly)
        {
            List<string> result = new List<string>();
            IEnumerable<EditableScriptData> values = m_scriptData.Values;
            if (simpleModeOnly) values = values.Where(v => v.IsVisibleInSimpleMode);
            foreach (EditableScriptData data in values)
            {
                if (!result.Contains(data.Category)) result.Add(data.Category);
            }
            return result;
        }

        internal Dictionary<string, EditableScriptData> ScriptData
        {
            get { return m_scriptData; }
        }

        internal IScript Clone(IScript script)
        {
            return m_scriptFactory.CreateSimpleScript(script.Save());
        }
    }
}
