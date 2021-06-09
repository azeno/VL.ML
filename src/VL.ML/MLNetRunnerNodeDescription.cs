﻿using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.Linq;
using VL.Core;
using System.IO;
using VL.Core.Diagnostics;
using System.Reactive.Linq;

namespace VL.ML
{
    /// <summary>
    /// Abstract ML.NET runner node
    /// </summary>
    class MLNetRunnerNodeDescription : IVLNodeDescription, IInfo
    {
        // Fields
        bool FInitialized;
        bool FError;
        string FSummary;
        string FPath;
        string FFullName;
        string FFriendlyName;

        public string FModelType { get; set; }
        private DataViewSchema predictionPipeline;
        
        public ITransformer TrainedModel { get; set; }

        // I/O
        List<PinDescription> inputs = new List<PinDescription>();
        List<PinDescription> outputs = new List<PinDescription>();

        public MLNetRunnerNodeDescription(IVLNodeDescriptionFactory factory, string path)
        {
            Factory = factory;
            FPath = path;
            FFullName = Path.GetFileNameWithoutExtension(path);
            FFriendlyName = FFullName.Split('_')[0];
            FModelType = FFullName.Split('_')[1];
            Name = FFriendlyName;

            MLContext = new MLContext();
            TrainedModel = MLContext.Model.Load(FPath, out predictionPipeline);
        }

        /// <summary>
        /// Retrieves relevant input and output from the trained model
        /// and creates corresponding pins
        /// </summary>
        /// <remarks>
        /// This does not map straight to the dynamic types
        /// </remarks>
        void Init()
        {
            if (FInitialized)
                return;

            // Look at the trained model and create input pins
            try
            {
                #region Create inputs and outputs
                Type type = typeof(object);
                object dflt = "";
                string descr = "";

                if(FModelType == "Classification")
                {
                    // Retrieve input column
                    // Our dataset must have a column named input
                    var inputColumn = predictionPipeline.FirstOrDefault(i => i.Name == "Input");
                    GetTypeDefaultAndDescription(inputColumn, ref type, ref dflt, ref descr);
                    inputs.Add(new PinDescription(inputColumn.Name, type, dflt, descr));

                    // Retrieve outputs
                    // For now we just retrieve the PredictedLabel and not the scores, since there seem to be issues using an IEnumerable output
                    var predictedLabelColumn = TrainedModel.GetOutputSchema(predictionPipeline).FirstOrDefault(o => o.Name == "PredictedLabel");
                    GetTypeDefaultAndDescription(predictedLabelColumn, ref type, ref dflt, ref descr);
                    outputs.Add(new PinDescription("Predicted Label", type, dflt, descr));

                    //var scoresColumn = TrainedModel.GetOutputSchema(predictionPipeline).FirstOrDefault(o => o.Name == "Score");
                    //GetTypeDefaultAndDescription(scoresColumn, ref type, ref dflt, ref descr);
                    //outputs.Add(new PinDescription("Score", type, dflt, descr));
                }
                else if(FModelType == "Regression")
                {
                    // Retrieve all inputs
                    // Need to find a way to discard the Label from the inputs
                    foreach (var input in predictionPipeline)
                    {
                        GetTypeDefaultAndDescription(input, ref type, ref dflt, ref descr);
                        inputs.Add(new PinDescription(input.Name, type, dflt, descr));
                    }

                    // Retrieve outputs
                    var scoreColumn = TrainedModel.GetOutputSchema(predictionPipeline).FirstOrDefault(o => o.Name == "Score");
                    GetTypeDefaultAndDescription(scoreColumn, ref type, ref dflt, ref descr);
                    outputs.Add(new PinDescription(scoreColumn.Name, type, dflt, descr));
                }
                else if(FModelType == "ImageClassification")
                {
                    // Soon
                }
                else
                {
                    // Unknown model type
                }

                
                // Add the Trigger input that will allow to trigger the node
                inputs.Add(new PinDescription("Predict", typeof(bool), false, "Runs a prediction every frame as long as enabled"));
                #endregion Create inputs and outputs

                FSummary = String.Format("Runs the ML.NET {0} {1} pre-trained model",FFriendlyName, FModelType);
                FInitialized = true;
            }
            catch (Exception e)
            {
                FError = true;
                Console.WriteLine("Error loading ML Model");
                Console.WriteLine(e.Message);
            }
        }

        public IVLNodeDescriptionFactory Factory { get; }



        public string Name { get; }
        public string Category => "ML.MLNet";
        public bool Fragmented => false;

        /// <summary>
        /// Returns the MLContext
        /// </summary>
        public MLContext MLContext { get; set; }

        /// <summary>
        /// Returns the prediction pipeline
        /// </summary>
        public DataViewSchema PredictionPipeline
        {
            get
            {
                return predictionPipeline;
            }
        }

        /// <summary>
        /// Returns the input pins
        /// </summary>
        public IReadOnlyList<IVLPinDescription> Inputs
        {
            get
            {
                Init();
                return inputs;
            }
        }

        /// <summary>
        /// Returns the output pins
        /// </summary>
        public IReadOnlyList<IVLPinDescription> Outputs
        {
            get
            {
                Init();
                return outputs;
            }
        }

        /// <summary>
        /// Displays a warning on the node if something goes wrong
        /// </summary>
        public IEnumerable<Core.Diagnostics.Message> Messages
        {
            get
            {
                if (FError)
                    yield return new Message(MessageType.Warning, "Brrrrr");
                else
                    yield break;
            }
        }

        private void GetTypeDefaultAndDescription(dynamic pin, ref Type type, ref object dflt, ref string descr)
        {
            descr = pin.Name;

            if (pin.Type.ToString() == "String")
            {
                type = typeof(string);
                dflt = "";
            }
            else if (pin.Type.ToString() == "Single")
            {
                type = typeof(float);
                dflt = 0.0f;
            }
            //else if (pin.Type == "System.Single[]")
            //{
            //    type = typeof(IEnumerable<float>);
            //    dflt = Enumerable.Repeat<float>(0, 0).ToArray();
            //}
            //else if (pin.Type.ToString() == "Vector<Single, 4>")
            //{
            //    type = typeof(IEnumerable<float>);
            //    dflt = Enumerable.Repeat<float>(0, 0).ToArray();
            //}
        }

        public string Summary => FSummary;
        public string Remarks => "";
        public IObservable<object> Invalidated => Observable.Empty<object>();

        public IVLNode CreateInstance(NodeContext context)
        {
            return new MyNode(this, context);
        }

        public bool OpenEditor()
        {
            // nope
            return true;
        }
    }
}