/*
 * Copyright (c) 2021 GVoid (Pascal Gerner)
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using CTasks.Exec;

namespace CTasks.Task.Ext
{
    public class CustomTaskStack : TaskStack {
        public Handler mHandler;
        public Handler mPostHandler;
        public bool mShutdown;

        public CustomTaskStack() : this(null, null, null) {}
        public CustomTaskStack(Handler handler, Handler postHandler) : this(handler, postHandler, null) {}
        protected CustomTaskStack(Handler handler, Handler postHandler, object lockObj) : base(lockObj) {
            mHandler = handler;
            mPostHandler = postHandler;
            mShutdown = false;
        }

        public override void Close() {
            try {
                base.Close();
            } finally {
                Handler handler = mHandler;
                mHandler = mPostHandler = null;

                if (handler != null
                        && mShutdown) {
                    try {
                        handler.Close();
                    } catch {}
                }
            }
        }

        protected override void OnExecute(Executable exec) {
            lock (mLock) {
                ThrowIfClosed();
                Handler handler = mHandler;
                if (handler == null) {
                    throw new NullReferenceException("No handler attached");
                }
                handler.Post(exec.ToGExec<Request>());
            }
        }

        protected override void OnPostExecute(Executable exec) {
            lock (mLock) {
                ThrowIfClosed();
                Handler postHandler = mPostHandler;
                if (postHandler == null) {
                    throw new NullReferenceException("No post handler attached");
                }
                postHandler.Post(exec.ToGExec<Request>());
            }
        }

        /* -------- Initialization -------- */
        public static CustomTaskStack With(Handler handler = null,
                                           Handler postHandler = null,
                                           bool isShared = true) {
            CustomTaskStack stack = new CustomTaskStack();
            stack.mHandler = handler;
            stack.mPostHandler = postHandler;
            stack.mShutdown = !isShared;
            return stack;
        }
    }

    public class CustomTaskSpawner : TaskSpawner {
        public Handler mHandler;
        public Handler mPostHandler;
        public bool mShutdown;

        public CustomTaskSpawner() : this(null, null, null) {}
        public CustomTaskSpawner(Handler handler, Handler postHandler) : this(handler, postHandler, null) {}
        protected CustomTaskSpawner(Handler handler, Handler postHandler, object lockObj) : base(lockObj) {
            mHandler = handler;
            mPostHandler = postHandler;
            mShutdown = false;
        }

        public override void Close() {
            try {
                base.Close();
            } finally {
                Handler handler = mHandler;
                mHandler = mPostHandler = null;

                if (handler != null
                        && mShutdown) {
                    try {
                        handler.Close();
                    } catch {}
                }
            }
        }

        protected override void OnExecute(Executable exec) {
            lock (mLock) {
                ThrowIfClosed();
                Handler handler = mHandler;
                if (handler == null) {
                    throw new NullReferenceException("No handler attached");
                }
                handler.Post(exec.ToGExec<Request>());
            }
        }

        protected override void OnPostExecute(Executable exec) {
            lock (mLock) {
                ThrowIfClosed();
                Handler postHandler = mPostHandler;
                if (postHandler == null) {
                    throw new NullReferenceException("No post handler attached");
                }
                postHandler.Post(exec.ToGExec<Request>());
            }
        }

        /* -------- Initialization -------- */
        public static CustomTaskSpawner With(Handler handler = null,
                                             Handler postHandler = null,
                                             bool isShared = true) {
            CustomTaskSpawner spawner = new CustomTaskSpawner();
            spawner.mHandler = handler;
            spawner.mPostHandler = postHandler;
            spawner.mShutdown = !isShared;
            return spawner;
        }
    }
}