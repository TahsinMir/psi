﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Psi.Visualization.VisualizationObjects
{
    using System;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.Serialization;
    using GalaSoft.MvvmLight.Command;
    using Microsoft.Psi;
    using Microsoft.Psi.Data;
    using Microsoft.Psi.Visualization.Collections;
    using Microsoft.Psi.Visualization.Common;
    using Microsoft.Psi.Visualization.Data;
    using Microsoft.Psi.Visualization.Helpers;
    using Microsoft.Psi.Visualization.Navigation;

    /// <summary>
    /// Represents a stream visualization object.
    /// </summary>
    /// <typeparam name="TData">The type of the stream.</typeparam>
    public abstract class StreamVisualizationObject<TData> : VisualizationObject, IStreamVisualizationObject
    {
        /// <summary>
        /// The stream being visualized.
        /// </summary>
        private StreamBinding streamBinding;

        /// <summary>
        /// The current (based on navigation cursor) value of the stream.
        /// </summary>
        private Message<TData>? currentValue;

        /// <summary>
        /// The data read from the stream.
        /// </summary>
        private ObservableKeyedCache<DateTime, Message<TData>>.ObservableKeyedView data;

        /// <summary>
        /// Indicates whether the store that is the source of data for this visualization object is a live store.
        /// </summary>
        private bool isLive = false;

        /// <summary>
        /// The snap to stream command.
        /// </summary>
        private RelayCommand toggleSnapToStreamCommand;

        /// <summary>
        /// The zoom to stream command.
        /// </summary>
        private RelayCommand zoomToStreamCommand;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamVisualizationObject{TData}"/> class.
        /// </summary>
        public StreamVisualizationObject()
        {
            this.IsShared = typeof(TData).IsGenericType && typeof(TData).GetGenericTypeDefinition() == typeof(Shared<>);
        }

        /// <inheritdoc/>
        [Browsable(false)]
        [IgnoreDataMember]
        public override bool CanSnapToStream => this.IsBound;

        /// <summary>
        /// Gets the snap to stream command.
        /// </summary>
        [Browsable(false)]
        [IgnoreDataMember]
        public RelayCommand ToggleSnapToStreamCommand
        {
            get
            {
                if (this.toggleSnapToStreamCommand == null)
                {
                    this.toggleSnapToStreamCommand = new RelayCommand(() => this.ToggleSnapToStream());
                }

                return this.toggleSnapToStreamCommand;
            }
        }

        /// <summary>
        /// Gets the zoom to stream command.
        /// </summary>
        [Browsable(false)]
        [IgnoreDataMember]
        public RelayCommand ZoomToStreamCommand
        {
            get
            {
                if (this.zoomToStreamCommand == null)
                {
                    this.zoomToStreamCommand = new RelayCommand(() => this.Container.Navigator.Zoom(this.StreamBinding.StreamMetadata.FirstMessageOriginatingTime, this.StreamBinding.StreamMetadata.LastMessageOriginatingTime));
                }

                return this.zoomToStreamCommand;
            }
        }

        /// <summary>
        /// Gets the current value.
        /// </summary>
        [Browsable(false)]
        [IgnoreDataMember]
        public Message<TData>? CurrentValue => this.currentValue;

        /// <summary>
        /// Gets the current data.
        /// </summary>
        [Browsable(false)]
        [IgnoreDataMember]
        public TData CurrentData => this.currentValue.HasValue ? this.currentValue.Value.Data : default;

        /// <summary>
        /// Gets the originating time of the current data.
        /// </summary>
        [Browsable(false)]
        [IgnoreDataMember]
        public DateTime CurrentOriginatingTime => this.currentValue.HasValue ? this.currentValue.Value.OriginatingTime : default;

        /// <summary>
        /// Gets or sets the data view.
        /// </summary>
        [Browsable(false)]
        [IgnoreDataMember]
        public ObservableKeyedCache<DateTime, Message<TData>>.ObservableKeyedView Data
        {
            get => this.data;
            protected set
            {
                if (this.data != value)
                {
                    if (this.data != null)
                    {
                        this.data.DetailedCollectionChanged -= this.OnDataDetailedCollectionChanged;
                    }

                    this.Set(nameof(this.Data), ref this.data, value);

                    if (this.data != null)
                    {
                        this.data.DetailedCollectionChanged += this.OnDataDetailedCollectionChanged;
                        this.OnDataCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                    }
                    else
                    {
                        this.SetCurrentValue(null);
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the stream binding.
        /// </summary>
        [Browsable(false)]
        [DataMember]
        public StreamBinding StreamBinding
        {
            get { return this.streamBinding; }
            set { this.Set(nameof(this.StreamBinding), ref this.streamBinding, value); }
        }

        /// <summary>
        /// Gets the adapter type.
        /// </summary>
        [Browsable(true)]
        [IgnoreDataMember]
        public Type StreamAdapterType => this.StreamBinding?.StreamAdapterType;

        /// <summary>
        /// Gets the summarizer type.
        /// </summary>
        [Browsable(true)]
        [IgnoreDataMember]
        public Type SummarizerType => this.StreamBinding?.SummarizerType;

        /// <summary>
        /// Gets a value indicating whether the visualization object is currently bound to a datasource.
        /// </summary>
        [Browsable(false)]
        [IgnoreDataMember]
        public bool IsBound => this.StreamBinding != null ? this.StreamBinding.IsBound : false;

        /// <inheritdoc/>
        public override bool ShowZoomToStreamMenuItem => this.IsBound;

        /// <inheritdoc/>
        [Browsable(false)]
        [IgnoreDataMember]
        public override string IconSource
        {
            get
            {
                if (this.StreamBinding == null || !this.StreamBinding.IsBound)
                {
                    return IconSourcePath.StreamUnbound;
                }
                else if (this.IsSnappedToStream)
                {
                    return this.IsLive ? IconSourcePath.SnapToStreamLive : IconSourcePath.SnapToStream;
                }
                else
                {
                    return this.IsLive ? IconSourcePath.StreamLive : IconSourcePath.Stream;
                }
            }
        }

        /// <inheritdoc/>
        [Browsable(false)]
        [IgnoreDataMember]
        public override bool IsSnappedToStream => this.Container != null ? this.Container.SnapToVisualizationObject == this : false;

        /// <summary>
        /// Gets the text to display in the snap to stream menu item.
        /// </summary>
        [Browsable(false)]
        [IgnoreDataMember]
        public string ToggleSnapToStreamCommandText => this.IsSnappedToStream ? "Unsnap From Stream" : "Snap To Stream";

        /// <inheritdoc />
        [Browsable(false)]
        [IgnoreDataMember]
        public bool IsLive
        {
            get => this.isLive;

            set
            {
                if (this.isLive != value)
                {
                    this.RaisePropertyChanging(nameof(this.IconSource));
                    this.isLive = value;
                    this.RaisePropertyChanged(nameof(this.IconSource));
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether type parameter T is Shared{} or not.
        /// </summary>
        protected bool IsShared { get; private set; }

        /// <summary>
        /// Sets the current value for the visualization object.
        /// </summary>
        /// <param name="value">The value to use as the new current value.</param>
        /// <param name="incrementSharedRefCount">True if the reference count of shared data should be automatically incremented, otherwise false.</param>
        public void SetCurrentValue(Message<TData>? value, bool incrementSharedRefCount = true)
        {
            if (this.currentValue != value)
            {
                this.RaisePropertyChanging(nameof(this.CurrentValue));
                this.RaisePropertyChanging(nameof(this.CurrentData));
                this.RaisePropertyChanging(nameof(this.CurrentOriginatingTime));

                // If we're handling shared objects, decrement the reference count of the current value
                if (this.IsShared && this.currentValue.HasValue)
                {
                    ((IDisposable)this.currentValue.Value.Data)?.Dispose();
                }

                // If we're handling shared objects, increment the reference count if requested.
                // NOTE: InstantVisualzationObject will pass false for incrementSharedRefCount
                // because it already increased the reference count itself before calling us.
                if (this.IsShared && incrementSharedRefCount)
                {
                    this.currentValue = value.DeepClone();
                }
                else
                {
                    this.currentValue = value;
                }

                this.RaisePropertyChanged(nameof(this.CurrentValue));
                this.RaisePropertyChanged(nameof(this.CurrentData));
                this.RaisePropertyChanged(nameof(this.CurrentOriginatingTime));
            }
        }

        /// <inheritdoc/>
        public override void ToggleSnapToStream()
        {
            this.RaisePropertyChanging(nameof(this.IsSnappedToStream));
            this.RaisePropertyChanging(nameof(this.IconSource));
            this.RaisePropertyChanging(nameof(this.ToggleSnapToStreamCommandText));

            // If this is already the visualization object being snapped to, then
            // reset snap to stream, otherwise set it to this visualization object.
            // If another object was previously snapped, then ask it to unsnap itself
            // so that the correct property changed event gets raised.
            if (this.Container.SnapToVisualizationObject == null)
            {
                this.Container.SnapToVisualizationObject = this;
            }
            else if (this.Container.SnapToVisualizationObject == this)
            {
                this.Container.SnapToVisualizationObject = null;
            }
            else
            {
                this.Container.SnapToVisualizationObject.ToggleSnapToStream();
                this.Container.SnapToVisualizationObject = this;
            }

            this.RaisePropertyChanged(nameof(this.IsSnappedToStream));
            this.RaisePropertyChanged(nameof(this.IconSource));
            this.RaisePropertyChanged(nameof(this.ToggleSnapToStreamCommandText));
        }

        /// <inheritdoc />
        public void UpdateStreamBinding(Session session)
        {
            // Keep a note of whether we're currently bound to a source
            bool wasBound = this.StreamBinding.IsBound;

            // Attempt to rebind to the underlying dataset
            StreamBindingResult bindingResult = this.StreamBinding.Update(session);

            // If nothing's changed, then we're done
            if (bindingResult == StreamBindingResult.BindingUnchanged)
            {
                return;
            }

            this.RaisePropertyChanging(nameof(this.IconSource));
            this.RaisePropertyChanging(nameof(this.CanSnapToStream));
            this.RaisePropertyChanging(nameof(this.ShowZoomToStreamMenuItem));

            // Notify that we're no longer bound to the previous data source
            if (wasBound)
            {
                this.OnStreamUnbound();
            }

            // Notify that we're now bound to a new data source
            if (bindingResult == StreamBindingResult.BoundToNewSource)
            {
                this.OnStreamBound();
            }

            this.RaisePropertyChanged(nameof(this.IconSource));
            this.RaisePropertyChanged(nameof(this.CanSnapToStream));
            this.RaisePropertyChanged(nameof(this.ShowZoomToStreamMenuItem));
        }

        /// <inheritdoc/>
        public virtual DateTime? GetSnappedTime(DateTime time, SnappingBehavior snappingBehavior = SnappingBehavior.Nearest)
        {
            // TODO
            return this.GetTimeOfNearestMessage(time, this.Data?.Count ?? 0, (idx) => this.Data[idx].OriginatingTime, snappingBehavior);
        }

        /// <summary>
        /// Returns the timestamp of the Message that's closest to currentTime.  Used by the "Snap To Stream" functionality.
        /// </summary>
        /// <param name="currentTime">The time underneath the mouse cursor.</param>
        /// <param name="count">Number of entries to search within.</param>
        /// <param name="timeAtIndex">Function that returns an index given a time.</param>
        /// <param name="snappingBehavior">Timeline snapping behavior.</param>
        /// <returns>The timestamp of the message that's temporally closest to currentTime.</returns>
        protected DateTime? GetTimeOfNearestMessage(DateTime currentTime, int count, Func<int, DateTime> timeAtIndex, SnappingBehavior snappingBehavior)
        {
            int index = IndexHelper.GetIndexForTime(currentTime, count, timeAtIndex, snappingBehavior);
            if (index >= 0)
            {
                return timeAtIndex(index);
            }

            return null;
        }

        /// <summary>
        /// Finds the index that is either exactly at currentTime, or closest to currentTime.
        /// Uses binary search to find exact match or location where match should be.
        /// </summary>
        /// <param name="currentTime">Time to search for.</param>
        /// <param name="count">Number of entries to search within.</param>
        /// <param name="timeAtIndex">Function that returns an index given a time.</param>
        /// <returns>Best matching index or -1 if no qualifying match was found.</returns>
        protected virtual int GetIndexForTime(DateTime currentTime, int count, Func<int, DateTime> timeAtIndex)
        {
            return IndexHelper.GetIndexForTime(currentTime, count, timeAtIndex);
        }

        /// <summary>
        /// Called when data collection contents have changed.
        /// </summary>
        /// <param name="e">Collection changed event arguments.</param>
        protected virtual void OnDataCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            // see if we are still active
            if (this.Container == null)
            {
                return;
            }

            if (this.Navigator.CursorMode == CursorMode.Live)
            {
                var last = this.Data.LastOrDefault();
                if (last != default(Message<TData>))
                {
                    this.SetCurrentValue(last);
                }
            }
        }

        /// <inheritdoc />
        protected override void OnRemoveFromPanel()
        {
            // Unbind the visualization object from any source
            this.UpdateStreamBinding(null);

            base.OnRemoveFromPanel();
        }

        /// <summary>
        /// Called when the stream visualization object is bound to a data source.
        /// </summary>
        protected virtual void OnStreamBound()
        {
        }

        /// <summary>
        /// Called when the stream visualization object is unbound from a data source.
        /// </summary>
        protected virtual void OnStreamUnbound()
        {
            this.Data = null;

            // If this is the stream currently being snapped to, disable snap to stream.
            if (this.IsSnappedToStream)
            {
                this.ToggleSnapToStream();
            }
        }

        private void OnDataDetailedCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            this.OnDataCollectionChanged(e);
        }
    }
}
