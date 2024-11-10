using Ryujinx.Common;
using System;

namespace Ryujinx.UI.Configuration
{

    class ReactiveObjectObserver<T> : IObjectObserver
        where T : IComparable
    {

        private readonly ReactiveObject<T> _reactiveObject;

        private T _initialValue;

        public bool HasChanged
        {
            get => (_initialValue == null && _reactiveObject.Value != null) || !_initialValue.Equals(_reactiveObject.Value);
        }

        public ReactiveObjectObserver(ReactiveObject<T> reactiveObject)
        {
            _reactiveObject = reactiveObject;
            _initialValue = _reactiveObject.Value;
        }

        public void Reset()
        {
            _initialValue = _reactiveObject.Value;
        }

        public void Destroy() { }

    }

}
