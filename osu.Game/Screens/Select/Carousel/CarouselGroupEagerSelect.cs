// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Screens.Select.Carousel
{
    /// <summary>
    /// A group which ensures at least one item is selected (if the group itself is selected).
    /// </summary>
    public class CarouselGroupEagerSelect : CarouselGroup
    {
        public CarouselGroupEagerSelect()
        {
            State.ValueChanged += state =>
            {
                if (state.NewValue == CarouselItemState.Selected)
                    attemptSelection();
            };
        }

        /// <summary>
        /// The last selected item.
        /// </summary>
        protected CarouselItem LastSelected { get; private set; }

        /// <summary>
        /// We need to keep track of the index for cases where the selection is removed but we want to select a new item based on its old location.
        /// </summary>
        private int lastSelectedIndex;

        /// <summary>
        /// To avoid overhead during filter operations, we don't attempt any selections until after all
        /// items have been filtered. This bool will be true during the base <see cref="Filter(FilterCriteria)"/>
        /// operation.
        /// </summary>
        private bool filteringItems;

        public override void Filter(FilterCriteria criteria)
        {
            filteringItems = true;
            base.Filter(criteria);
            filteringItems = false;

            attemptSelection();
        }

        public override void RemoveItem(CarouselItem i)
        {
            base.RemoveItem(i);

            if (i != LastSelected)
                updateSelectedIndex();
        }

        private bool addingItems;

        public void AddItems(IEnumerable<CarouselItem> items)
        {
            addingItems = true;

            foreach (var i in items)
                AddItem(i);

            addingItems = false;

            attemptSelection();
        }

        public override void AddItem(CarouselItem i)
        {
            base.AddItem(i);
            if (!addingItems)
                attemptSelection();
        }

        protected override void ChildItemStateChanged(CarouselItem item, CarouselItemState value)
        {
            base.ChildItemStateChanged(item, value);

            switch (value)
            {
                case CarouselItemState.Selected:
                    updateSelected(item);
                    break;

                case CarouselItemState.NotSelected:
                case CarouselItemState.Collapsed:
                    attemptSelection();
                    break;
            }
        }

        private void attemptSelection()
        {
            if (filteringItems) return;

            // we only perform eager selection if we are a currently selected group.
            if (State.Value != CarouselItemState.Selected) return;

            // we only perform eager selection if none of our items are in a selected state already.
            if (Items.Any(i => i.State.Value == CarouselItemState.Selected)) return;

            PerformSelection();
        }

        /// <summary>
        /// Finds the item this group would select next if it attempted selection
        /// </summary>
        /// <returns>An unfiltered item nearest to the last selected one or null if all items are filtered</returns>
        protected virtual CarouselItem GetNextToSelect()
        {
            int forwardsIndex = lastSelectedIndex;
            int backwardsIndex = lastSelectedIndex;

            while (true)
            {
                // check if a direction has been exhausted and an item (or null) from the other direction should be returned
                if (forwardsIndex >= Children.Count)
                    return Children.Reverse().Skip(Children.Count - backwardsIndex - 1).FirstOrDefault(item => !item.Filtered.Value);
                if (backwardsIndex < 0)
                    return Children.Skip(forwardsIndex).FirstOrDefault(item => !item.Filtered.Value);

                // check if an unfiltered item has been reached
                if (!Children[forwardsIndex].Filtered.Value)
                    return Children[forwardsIndex];
                if (!Children[backwardsIndex].Filtered.Value)
                    return Children[backwardsIndex];

                // increment the indices
                forwardsIndex++;
                backwardsIndex--;
            }
        }

        protected virtual void PerformSelection()
        {
            CarouselItem nextToSelect = GetNextToSelect();

            if (nextToSelect != null)
                nextToSelect.State.Value = CarouselItemState.Selected;
            else
                updateSelected(null);
        }

        private void updateSelected(CarouselItem newSelection)
        {
            if (newSelection != null)
                LastSelected = newSelection;
            updateSelectedIndex();
        }

        private void updateSelectedIndex() => lastSelectedIndex = LastSelected == null ? 0 : Math.Max(0, GetIndexOfItem(LastSelected));
    }
}
