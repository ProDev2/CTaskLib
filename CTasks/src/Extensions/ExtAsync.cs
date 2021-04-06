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
using System.Threading.Tasks;
using CTasks.Exec;

namespace CTasks.Task.Ext
{
    public class AsyncTaskStack : ExecutorTaskStack {
        public Handler mHandler;

        public AsyncTaskStack() : this(null, null, null, null) {}
        public AsyncTaskStack(TaskFactory factory) : this(factory, null, null, null) {}
        public AsyncTaskStack(TaskFactory factory, TaskCreationOptions? options) : this(factory, options, null, null) {}
        protected AsyncTaskStack(TaskFactory factory, TaskCreationOptions? options, Handler handler, object lockObj) 
            : base(factory, options, lockObj) {
            mHandler = handler;
        }

        public override void Close() {
            try {
                base.Close();
            } finally {
                mHandler = null;
            }
        }

        protected override void OnPostExecute(Executable exec) {
            lock (mLock) {
                ThrowIfClosed();
                Handler handler = mHandler;
                if (handler == null) {
                    throw new NullReferenceException("No handler attached");
                }
                handler.Post(exec.ToGExec<Request>());
            }
        }

        /* -------- Initialization -------- */
        public static AsyncTaskStack With(TaskFactory factory = null, 
                                          TaskCreationOptions? options = null, 
                                          Handler handler = null) {
            if (factory == null) {
                factory = System.Threading.Tasks.Task.Factory;
            }

            AsyncTaskStack stack = new AsyncTaskStack();
            stack.mFactory = factory;
            stack.mOptions = options ?? TaskCreationOptions.None;
            stack.mHandler = handler;
            return stack;
        }
    }

    public class AsyncTaskSpawner : ExecutorTaskSpawner {
        public Handler mHandler;

        public AsyncTaskSpawner() : this(null, null, null, null) {}
        public AsyncTaskSpawner(TaskFactory factory) : this(factory, null, null, null) {}
        public AsyncTaskSpawner(TaskFactory factory, TaskCreationOptions? options) : this(factory, options, null, null) {}
        protected AsyncTaskSpawner(TaskFactory factory, TaskCreationOptions? options, Handler handler, object lockObj) 
            : base(factory, options, lockObj) {
            mHandler = handler;
        }

        public override void Close() {
            try {
                base.Close();
            } finally {
                mHandler = null;
            }
        }

        protected override void OnPostExecute(Executable exec) {
            lock (mLock) {
                ThrowIfClosed();
                Handler handler = mHandler;
                if (handler == null) {
                    throw new NullReferenceException("No handler attached");
                }
                handler.Post(exec.ToGExec<Request>());
            }
        }

        /* -------- Initialization -------- */
        public static AsyncTaskSpawner With(TaskFactory factory = null, 
                                            TaskCreationOptions? options = null, 
                                            Handler handler = null) {
            if (factory == null) {
                factory = System.Threading.Tasks.Task.Factory;
            }

            AsyncTaskSpawner spawner = new AsyncTaskSpawner();
            spawner.mFactory = factory;
            spawner.mOptions = options ?? TaskCreationOptions.None;
            spawner.mHandler = handler;
            return spawner;
        }
    }
}