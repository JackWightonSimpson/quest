﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AxeSoftware.Quest
{
    public class EditableDictionary<T> : IEditableDictionary<T>, IDataWrapper
    {
        public event EventHandler<EditableListUpdatedEventArgs<T>> Added;
        public event EventHandler<EditableListUpdatedEventArgs<T>> Removed;

        // currently unused
        public event EventHandler<DataWrapperUpdatedEventArgs> UnderlyingValueUpdated
        {
            add { }
            remove { }
        }

        // currently unused - we currently only use EditableDictionary<string>, and we don't
        // use the Updated event as the same behaviour is implemented with a combination of Added and Removed.
        public event EventHandler<EditableListUpdatedEventArgs<T>> Updated
        {
            add { }
            remove { }
        }

        #region Static DataWrapper
        private static EditableDataWrapper<QuestDictionary<T>, EditableDictionary<T>> s_wrapper;

        static EditableDictionary()
        {
            s_wrapper = new EditableDataWrapper<QuestDictionary<T>, EditableDictionary<T>>(GetNewInstance);
        }

        public static EditableDictionary<T> GetInstance(EditorController controller, QuestDictionary<T> list)
        {
            return s_wrapper.GetInstance(controller, list);
        }

        private static EditableDictionary<T> GetNewInstance(EditorController controller, QuestDictionary<T> list)
        {
            return new EditableDictionary<T>(controller, list);
        }
        #endregion

        private QuestDictionary<T> m_source;
        private Dictionary<string, IEditableListItem<T>> m_wrappedItems = new Dictionary<string, IEditableListItem<T>>();
        private EditorController m_controller;

        public EditableDictionary(EditorController controller, QuestDictionary<T> source)
        {
            m_controller = controller;
            m_source = source;
            m_source.Added += m_source_Added;
            m_source.Removed += m_source_Removed;
            PopulateWrappedItems();
        }

        private void PopulateWrappedItems()
        {
            m_wrappedItems.Clear();
            int index = 0;

            foreach (var item in m_source)
            {
                AddWrappedItem(item.Key, item.Value, EditorUpdateSource.System, index);
                index++;
            }
        }

        public object GetUnderlyingValue()
        {
            return m_source;
        }

        public IDictionary<string, IEditableListItem<T>> Items
        {
            get { return m_wrappedItems; }
        }

        private void AddWrappedItem(string key, T value, EditorUpdateSource source, int index)
        {
            IEditableListItem<T> wrappedValue = new EditableListItem<T>(key, value);
            m_wrappedItems.Add(key, wrappedValue);

            if (Added != null) Added(this, new EditableListUpdatedEventArgs<T> { UpdatedItem = wrappedValue, Index = index, Source = source });
        }

        private void RemoveWrappedItem(IEditableListItem<T> item, EditorUpdateSource source, int index)
        {
            m_wrappedItems.Remove(item.Key);
            if (Removed != null) Removed(this, new EditableListUpdatedEventArgs<T> { UpdatedItem = item, Index = index, Source = source });
        }

        void m_source_Added(object sender, QuestDictionaryUpdatedEventArgs<T> e)
        {
            AddWrappedItem(e.Key, e.Item, (EditorUpdateSource)e.Source, e.Index);
        }

        void m_source_Removed(object sender, QuestDictionaryUpdatedEventArgs<T> e)
        {
            RemoveWrappedItem(m_wrappedItems[e.Key], (EditorUpdateSource)e.Source, e.Index);
        }

        public IEnumerable<KeyValuePair<string, string>> DisplayItems
        {
            get
            {
                Dictionary<string, string> result = new Dictionary<string, string>();

                foreach (var item in m_wrappedItems)
                {
                    // TO DO: We will need some kind of projection function for non-string T's
                    result.Add(item.Key, item.Value.Value as string);
                }

                return result;
            }
        }

        public void Add(string key, T value)
        {
            string undoEntry = null;
            if (typeof(T) == typeof(string))
            {
                undoEntry = string.Format("Add '{0}={1}'", key, value as string);
            }

            if (undoEntry == null)
            {
                throw new InvalidOperationException("Unknown dictionary type");
            }

            m_controller.WorldModel.UndoLogger.StartTransaction(undoEntry);
            m_source.Add(key, value, UpdateSource.User);
            m_controller.WorldModel.UndoLogger.EndTransaction();
        }

        public void Remove(params string[] keys)
        {
            string undoEntry = null;
            if (typeof(T) == typeof(string))
            {
                undoEntry = string.Format("Remove '{0}'", string.Join(",", keys));
            }

            if (undoEntry == null)
            {
                throw new InvalidOperationException("Unknown list type");
            }

            m_controller.WorldModel.UndoLogger.StartTransaction(undoEntry);
            foreach (string key in keys)
            {
                m_source.Remove(key, UpdateSource.User);
            }
            m_controller.WorldModel.UndoLogger.EndTransaction();
        }

        public ValidationResult CanAdd(string key)
        {
            if (m_source.ContainsKey(key))
            {
                return new ValidationResult { Valid = false, Message = ValidationMessage.ItemAlreadyExists };
            }
            return new ValidationResult { Valid = true };
        }

        public T this[string key]
        {
            get
            {
                return m_source[key];
            }
        }

        public void Update(string key, T value)
        {
            string undoEntry = null;
            if (typeof(T) == typeof(string))
            {
                undoEntry = string.Format("Update '{0}='{1}'", key, value as string);
            }

            if (undoEntry == null)
            {
                throw new InvalidOperationException("Unknown list type");
            }

            m_controller.WorldModel.UndoLogger.StartTransaction(undoEntry);
            int index = m_source.IndexOfKey(key);
            m_source.Remove(key, UpdateSource.User);
            m_source.Add(key, value, UpdateSource.User, index);
            m_controller.WorldModel.UndoLogger.EndTransaction();
        }

        public string DisplayString()
        {
            throw new NotImplementedException();
        }
    }
}
