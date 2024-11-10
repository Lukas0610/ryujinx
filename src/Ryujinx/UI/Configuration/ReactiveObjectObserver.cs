using Ryujinx.Common;
using System;

namespace Ryujinx.UI.Configuration
{

    class ReactiveObjectObserver
    {

        public static ReactiveObjectObserver<T> Create<T>(ReactiveObject<T> reactiveObject)
            where T : IComparable
        {
            return new ReactiveObjectObserver<T>(reactiveObject);
        }

    }

}
