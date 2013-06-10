#region Copyright 2010 by Roger Knapp, Licensed under the Apache License, Version 2.0
/* Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using JiraSVN.Common.Interfaces;

namespace JiraSVN.Plugin.UI
{
	/// <summary>
	/// A simple binding list interface IBindingList[T] that doubles as an IList[T]
	/// </summary>
	/// <typeparam name="T"></typeparam>
	[ComVisible(false)]
	public interface IBindingList<T> : IList<T>, IBindingList
	{
	}

	[ComVisible(false)]
	internal class DataBindingList<T> : BindingList<T>, IBindingList<T>, IComparer<T>
		where T : class, IIdentifiable
	{
		private readonly Dictionary<string, int> _indexes;
		private readonly bool _sorted;
		private int _selectedIndex, _lastSelectedIndex;
		private string _selectedText;

		public DataBindingList(bool sorted)
		{
			_selectedIndex = _lastSelectedIndex = -1;
			_sorted = sorted;
			_indexes = new Dictionary<string, int>();
		}

		public int SelectedIndex
		{
			get { return (_selectedIndex >= 0 && _selectedIndex < Count) ? _selectedIndex : -1; }
			set
			{
				_selectedIndex = (value >= 0 && value < Count) ? value : -1;
				_selectedText = SelectedText;
			}
		}

		public T SelectedItem
		{
			get
			{
				T sel;
				if (TryGetSelection(out sel))
					return sel;
				return null;
			}
			set
			{
				int index;
				if (value != null && _indexes.TryGetValue(value.Id, out index))
					_selectedIndex = index;
				else
					_selectedIndex = -1;
				_selectedText = SelectedText;
			}
		}

		public string SelectedText
		{
			get { return SelectedItem != null ? SelectedItem.Name : _selectedText; }
			set
			{
				int sel = SelectedIndex;
				for (int ix = 0; ix < Count; ix++)
				{
					if (this[ix].Name == value)
						sel = ix;
				}
				SelectedIndex = sel;
				_selectedText = value;
			}
		}

		public bool TryGetSelection(out T selection)
		{
			if (_selectedIndex >= 0 && _selectedIndex < Count)
			{
				selection = this[_selectedIndex];
				return true;
			}
			selection = null;
			return false;
		}

		public bool IsSelectionDirty
		{
			get { return _lastSelectedIndex != _selectedIndex; }
		}

		public void ClearSelectionDirty()
		{
			_lastSelectedIndex = _selectedIndex;
		}

		protected override void ClearItems()
		{
			_selectedIndex = -1;
			_indexes.Clear();
			base.ClearItems();
		}

		protected override void InsertItem(int index, T item)
		{
			base.InsertItem(index, item);
			_indexes[item.Id] = index;
		}

		protected override void RemoveItem(int index)
		{
			if (index >= 0 && index < Count)
				_indexes.Remove(this[index].Id);
			base.RemoveItem(index);
		}

		public new bool Contains(T item)
		{
			return _indexes.ContainsKey(item.Id);
		}

		[Obsolete]
		public new void Add(T item)
		{
		}

		public void AddRange(IEnumerable<T> items)
		{
			var newitems = new List<T>(this);

			foreach (T item in items)
			{
				if (!Contains(item))
					newitems.Add(item);
			}

			ReplaceContents(newitems);
		}

		public void ReplaceContents(IEnumerable<T> items)
		{
			RaiseListChangedEvents = false;
			try
			{
				if (_sorted)
				{
					var sorteditems = new List<T>(items);
					sorteditems.Sort(this);
					items = sorteditems;
				}

				T selected = SelectedItem;
				ClearItems();

				foreach (T item in items)
				{
					base.Add(item);
					if ((selected != null && item.Id == selected.Id) ||
					    (selected == null && StringComparer.InvariantCultureIgnoreCase.Equals(_selectedText, item.Name)))
						_selectedIndex = Count - 1;
				}
			}
			finally
			{
				RaiseListChangedEvents = true;
				_lastSelectedIndex = _selectedIndex;
				OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, 0));
			}
		}

		int IComparer<T>.Compare(T x, T y)
		{
			return StringComparer.InvariantCultureIgnoreCase.Compare(x.Name, y.Name);
		}
	}
}