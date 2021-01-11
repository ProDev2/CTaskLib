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

namespace CTasks
{
    public static class Converter {
        /* -------- To Task-Executable (From Executable) -------- */
        public static CTasks.Task.Executable ToTask(this CTasks.Exec.Executable exec)
            => () => exec();
        
        public static CTasks.Task.Executable<T> ToGTask<T>(this CTasks.Exec.Executable exec)
            => (obj) => {
                exec();
                return null;
            };
        
        public static CTasks.Task.Executable ToTask<T>(this CTasks.Exec.Executable<T> exec, T defObj = default(T))
            => () => exec(defObj);
        
        public static CTasks.Task.Executable<T> ToGTask<T>(this CTasks.Exec.Executable<T> exec)
            => (obj) => {
                exec(obj);
                return null;
            };
        
        public static CTasks.Task.Executable<R> ToGTask<T, R>(this CTasks.Exec.Executable<T> exec, T defObj = default(T))
            => (obj) => {
                exec(defObj);
                return null;
            };
        
        /* -------- To Exec-Executable (From Executable) -------- */
        public static CTasks.Exec.Executable ToExec(this CTasks.Task.Executable exec)
            => () => exec();
        
        public static CTasks.Exec.Executable<T> ToGExec<T>(this CTasks.Task.Executable exec)
            => (obj) => exec();
        
        public static CTasks.Exec.Executable ToExec<T>(this CTasks.Task.Executable<T> exec, T defObj = default(T))
            => () => exec(defObj);
        
        public static CTasks.Exec.Executable<T> ToGExec<T>(this CTasks.Task.Executable<T> exec)
            => (obj) => exec(obj);
        
        public static CTasks.Exec.Executable<R> ToGExec<T, R>(this CTasks.Task.Executable<T> exec, T defObj = default(T))
            => (obj) => exec(defObj);
        
        /* -------- To Task-Executable (From Handleable) -------- */
        public static CTasks.Task.Executable ToTask(this CTasks.Task.Handleable handle, int defState = 0, Exception[] defExceptions = null)
            => () => handle(defState, defExceptions);
        
        public static CTasks.Task.Executable<T> ToGTask<T>(this CTasks.Task.Handleable handle, int defState = 0, Exception[] defExceptions = null)
            => (obj) => {
                handle(defState, defExceptions);
                return null;
            };
        
        public static CTasks.Task.Executable ToTask(this CTasks.Exec.Handleable handle, int defState = 0, Exception[] defExceptions = null)
            => () => handle(defState, defExceptions);
        
        public static CTasks.Task.Executable<T> ToGTask<T>(this CTasks.Exec.Handleable handle, int defState = 0, Exception[] defExceptions = null)
            => (obj) => {
                handle(defState, defExceptions);
                return null;
            };
        
        /* -------- To Exec-Executable (From Handleable) -------- */
        public static CTasks.Exec.Executable ToExec(this CTasks.Task.Handleable handle, int defState = 0, Exception[] defExceptions = null)
            => () => handle(defState, defExceptions);
        
        public static CTasks.Exec.Executable<T> ToGExec<T>(this CTasks.Task.Handleable handle, int defState = 0, Exception[] defExceptions = null)
            => (obj) => handle(defState, defExceptions);
        
        public static CTasks.Exec.Executable ToExec(this CTasks.Exec.Handleable handle, int defState = 0, Exception[] defExceptions = null)
            => () => handle(defState, defExceptions);
        
        public static CTasks.Exec.Executable<T> ToGExec<T>(this CTasks.Exec.Handleable handle, int defState = 0, Exception[] defExceptions = null)
            => (obj) => handle(defState, defExceptions);
        
        /* -------- To Task-Handleable (From Handleable) -------- */
        public static CTasks.Task.Handleable ToTaskHandle(this CTasks.Exec.Handleable handle)
            => (state, exceptions) => handle(state, exceptions);
        
        public static CTasks.Task.Handleable ToTaskHandle(this CTasks.Exec.Handleable handle, int defState = 0, Exception[] defExceptions = null)
            => (state, exceptions) => handle(defState, defExceptions);
        
        /* -------- To Exec-Handleable (From Handleable) -------- */
        public static CTasks.Exec.Handleable ToExecHandle(this CTasks.Task.Handleable handle)
            => (state, exceptions) => handle(state, exceptions);
        
        public static CTasks.Exec.Handleable ToExecHandle(this CTasks.Task.Handleable handle, int defState = 0, Exception[] defExceptions = null)
            => (state, exceptions) => handle(defState, defExceptions);
        
        /* -------- To Task-Handleable (From Executable) -------- */
        public static CTasks.Task.Handleable ToTaskHandle(this CTasks.Task.Executable exec)
            => (state, exceptions) => exec();
        
        public static CTasks.Task.Handleable ToTaskHandle<T>(this CTasks.Task.Executable<T> exec, T defObj = default(T))
            => (state, exceptions) => exec(defObj);
        
        public static CTasks.Task.Handleable ToTaskHandle(this CTasks.Exec.Executable exec)
            => (state, exceptions) => exec();
        
        public static CTasks.Task.Handleable ToTaskHandle<T>(this CTasks.Exec.Executable<T> exec, T defObj = default(T))
            => (state, exceptions) => exec(defObj);
        
        /* -------- To Exec-Handleable (From Executable) -------- */
        public static CTasks.Exec.Handleable ToExecHandle(this CTasks.Task.Executable exec)
            => (state, exceptions) => exec();
        
        public static CTasks.Exec.Handleable ToExecHandle<T>(this CTasks.Task.Executable<T> exec, T defObj = default(T))
            => (state, exceptions) => exec(defObj);
        
        public static CTasks.Exec.Handleable ToExecHandle(this CTasks.Exec.Executable exec)
            => (state, exceptions) => exec();
        
        public static CTasks.Exec.Handleable ToExecHandle<T>(this CTasks.Exec.Executable<T> exec, T defObj = default(T))
            => (state, exceptions) => exec(defObj);
    }
}