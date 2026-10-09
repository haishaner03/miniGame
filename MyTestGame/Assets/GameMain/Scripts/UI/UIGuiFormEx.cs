using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;
using System;
using GameFramework.Event;
using GameFramework;

namespace Flower
{
    public class UGuiFormEx : UGuiForm
    {
        private EventSubscriber eventSubscriber;

        protected override void OnClose(bool isShutdown, object userData)
        {
            base.OnClose(isShutdown, userData);

            UnSubscribeAll();
            if (eventSubscriber != null)
            {
                ReferencePool.Release(eventSubscriber);
                eventSubscriber = null;
            }

        }

        protected void Subscribe(int id, EventHandler<GameEventArgs> handler)
        {
            if (eventSubscriber == null)
                eventSubscriber = EventSubscriber.Create(this);

            eventSubscriber.Subscribe(id, handler);
        }

        protected void UnSubscribe(int id, EventHandler<GameEventArgs> handler)
        {
            if (eventSubscriber != null)
                eventSubscriber.UnSubscribe(id, handler);
        }

        protected void UnSubscribeAll()
        {
            if (eventSubscriber != null)
                eventSubscriber.UnSubscribeAll();
        }

    }
}
