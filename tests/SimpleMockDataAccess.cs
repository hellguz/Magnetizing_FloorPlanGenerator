using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace FloorPlanGeneratorTests
{
    /// <summary>
    /// Simplified mock implementation focusing only on methods used by our algorithm
    /// </summary>
    public class SimpleMockDataAccess
    {
        private Dictionary<string, object> _inputData = new Dictionary<string, object>();
        private Dictionary<string, object> _outputData = new Dictionary<string, object>();
        private Dictionary<int, object> _inputDataByIndex = new Dictionary<int, object>();
        private Dictionary<int, object> _outputDataByIndex = new Dictionary<int, object>();

        // Public methods to set input data for testing
        public void SetInputData(string parameterName, object value)
        {
            _inputData[parameterName] = value;
        }

        public void SetInputData(int index, object value)
        {
            _inputDataByIndex[index] = value;
        }

        // Public methods to get output data after algorithm execution
        public T GetOutputData<T>(string parameterName)
        {
            if (_outputData.ContainsKey(parameterName))
                return (T)_outputData[parameterName];
            return default(T);
        }

        public T GetOutputData<T>(int index)
        {
            if (_outputDataByIndex.ContainsKey(index))
                return (T)_outputDataByIndex[index];
            return default(T);
        }

        // Core methods needed by algorithm
        public bool GetData<T>(int index, ref T destination)
        {
            if (_inputDataByIndex.ContainsKey(index))
            {
                destination = (T)_inputDataByIndex[index];
                return true;
            }
            return false;
        }

        public bool GetData<T>(string name, ref T destination)
        {
            if (_inputData.ContainsKey(name))
            {
                destination = (T)_inputData[name];
                return true;
            }
            return false;
        }

        public bool SetData(int index, object data)
        {
            _outputDataByIndex[index] = data;
            return true;
        }

        public bool SetData(string name, object data)
        {
            _outputData[name] = data;
            return true;
        }

        public bool SetDataList<T>(int index, IEnumerable<T> data)
        {
            _outputDataByIndex[index] = data;
            return true;
        }

        public bool SetDataList<T>(string name, IEnumerable<T> data)
        {
            _outputData[name] = data;
            return true;
        }

        public bool GetDataList<T>(int index, List<T> list)
        {
            if (_inputDataByIndex.ContainsKey(index) && _inputDataByIndex[index] is IEnumerable<T>)
            {
                list.AddRange((IEnumerable<T>)_inputDataByIndex[index]);
                return true;
            }
            return false;
        }

        public bool GetDataList<T>(string name, List<T> list)
        {
            if (_inputData.ContainsKey(name) && _inputData[name] is IEnumerable<T>)
            {
                list.AddRange((IEnumerable<T>)_inputData[name]);
                return true;
            }
            return false;
        }

        // Properties
        public int Iteration { get; set; } = 0;
        public int IterationCount { get; set; } = 1;
        public IGH_Component Owner { get; set; }
        public int InputCount { get; set; }
        public int OutputCount { get; set; }
    }
}