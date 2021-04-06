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

namespace CTasks.Task.Ext
{
    public class ExecutorTaskStack : TaskStack {
        public TaskFactory mFactory;
        public TaskCreationOptions mOptions;

        public ExecutorTaskStack() : this(null, null, null) {}
        public ExecutorTaskStack(TaskFactory factory) : this(factory, null, null) {}
        public ExecutorTaskStack(TaskFactory factory, TaskCreationOptions? options) : this(factory, options, null) {}
        protected ExecutorTaskStack(TaskFactory factory, TaskCreationOptions? options, object lockObj) : base(lockObj) {
            mFactory = factory;
            mOptions = options ?? TaskCreationOptions.None;
        }

        protected override void OnExecute(Executable exec) {
            lock (mLock) {
                ThrowIfClosed();
                TaskFactory factory = mFactory;
                if (factory == null) {
                    throw new NullReferenceException("No factory attached");
                }
                factory.StartNew(() => exec(), mOptions);
            }
        }

        /* -------- Initialization -------- */
        public static ExecutorTaskStack With(TaskFactory factory = null, 
                                             TaskCreationOptions? options = null) {
            if (factory == null) {
                factory = System.Threading.Tasks.Task.Factory;
            }

            ExecutorTaskStack stack = new ExecutorTaskStack();
            stack.mFactory = factory;
            stack.mOptions = options ?? TaskCreationOptions.None;
            return stack;
        }
    }

    public class ExecutorTaskSpawner : TaskSpawner {
        public TaskFactory mFactory;
        public TaskCreationOptions mOptions;

        public ExecutorTaskSpawner() : this(null, null, null) {}
        public ExecutorTaskSpawner(TaskFactory factory) : this(factory, null, null) {}
        public ExecutorTaskSpawner(TaskFactory factory, TaskCreationOptions? options) : this(factory, options, null) {}
        protected ExecutorTaskSpawner(TaskFactory factory, TaskCreationOptions? options, object lockObj) : base(lockObj) {
            mFactory = factory;
            mOptions = options ?? TaskCreationOptions.None;
        }

        protected override void OnExecute(Executable exec) {
            lock (mLock) {
                ThrowIfClosed();
                TaskFactory factory = mFactory;
                if (factory == null) {
                    throw new NullReferenceException("No factory attached");
                }
                factory.StartNew(() => exec(), mOptions);
            }
        }

        /* -------- Initialization -------- */
        public static ExecutorTaskSpawner With(TaskFactory factory = null, 
                                               TaskCreationOptions? options = null) {
            if (factory == null) {
                factory = System.Threading.Tasks.Task.Factory;
            }

            ExecutorTaskSpawner spawner = new ExecutorTaskSpawner();
            spawner.mFactory = factory;
            spawner.mOptions = options ?? TaskCreationOptions.None;
            return spawner;
        }
    }
}