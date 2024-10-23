using System;
using System.Collections.Generic;
using System.IO;
using Orts.Parsers.Msts;
using ORTS.Common;
using Tomlyn;

namespace Orts.Formats
{
    public class ConsistFile
    {
        public string Name { get; }
        public Train_Config Train { get; set; }
         
        public ConsistFile(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            
            switch (extension.ToLower())
            {
                case ".con":
                    using (var stf = new STFReader(filePath, false))
                        stf.ParseFile(new STFReader.TokenProcessor[] {
                            new STFReader.TokenProcessor("train", ()=>{ Train = new Train_Config(new SimisConsistReader(stf)); }),
                        });
                    break;

                case ".toml":
                    using (StreamReader fileReader = new StreamReader(filePath))
                        Train = new Train_Config(new TomlConsistReader(fileReader.ReadToEnd()));
                    break;
            }

            Name = Train.TrainCfg.Name;
        }
    }

    public class Train_Config
    {
        public TrainCfg TrainCfg { get; set; }

        public Train_Config(ConsistReader reader) {
            TrainCfg = new TrainCfg(reader);
        }
    }

    public class TrainCfg
    {
        // NOTE: "nextwagonuid" discarded as it does not seem to be used anywhere in the codebase

        public string Name { get; set; } = "Loose consist";
        public int Serial { get; set; } = 1;
        public MaxVelocity MaxVelocity { get; set; }
        public float Durability { get; set; } = 1.0f;
        public string TcsParametersFileName { get; set; } = string.Empty;

        public List<Wagon> WagonList { get; set; } = new List<Wagon>();

        public TrainCfg(ConsistReader reader) {
            reader.Parse(this);
        }
    }

    // NOTE: Should this be a struct?
    public class MaxVelocity
    {
        public float A { get; set; }
        public float B { get; set; }

        public MaxVelocity(float a, float b = 0.001f)
        {
            A = a;
            B = b;
        }
    }

    public class Wagon
    {
        public string Folder { get; set; }
        public string Name { get; set; }
        public int UiD { get; set; }
        public bool IsEngine { get; set; }
        public bool IsEOT { get; set; }
        public bool Flip { get; set; }
        public List<LoadData> LoadDataList { get; set; }

        public Wagon(WagonReader reader) {
            reader.Parse(this);
        }
    }

    public struct LoadData
    {
        public string Name;
        public string Folder;
        public LoadPosition LoadPosition;
        public LoadState LoadState;
    }

    public abstract class ConsistReader
    {
        public abstract void Parse(TrainCfg ctx); // "ctx" meaning the "context" of the parent class
    }

    public abstract class WagonReader
    {
        public abstract void Parse(Wagon ctx);
    }

    public class SimisConsistReader : ConsistReader {
        private STFReader stf;

        public SimisConsistReader(STFReader _stf)
        {
            stf = _stf;
        }

        public override void Parse(TrainCfg ctx)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("traincfg", ()=>{
                    stf.MustMatch("(");
                    ctx.Name = stf.ReadString();
                    stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("name", ()=>{ ctx.Name = stf.ReadStringBlock(null); }),
                        new STFReader.TokenProcessor("serial", ()=>{ ctx.Serial = stf.ReadIntBlock(null); }),
                        new STFReader.TokenProcessor("maxvelocity", ()=>{
                            stf.MustMatch("(");
                            ctx.MaxVelocity = new MaxVelocity(stf.ReadFloat(STFReader.UNITS.Speed, null), stf.ReadFloat(STFReader.UNITS.Speed, null));
                            stf.MustMatch(")");
                        }),
                        new STFReader.TokenProcessor("durability", ()=>{ ctx.Durability = stf.ReadFloatBlock(STFReader.UNITS.None, null); }),
                        new STFReader.TokenProcessor("wagon", ()=>{ ctx.WagonList.Add(new Wagon(new SimisWagonReader(stf))); }),
                        new STFReader.TokenProcessor("engine", ()=>{ ctx.WagonList.Add(new Wagon(new SimisWagonReader(stf))); }),
                        new STFReader.TokenProcessor("ortseot", ()=>{ ctx.WagonList.Add(new Wagon(new SimisWagonReader(stf))); }),
                        new STFReader.TokenProcessor("ortstraincontrolsystemparameters", () => ctx.TcsParametersFileName = stf.ReadStringBlock(null)),
                    });
                }),
            });
        }
    }

    public class SimisWagonReader : WagonReader
    {
        private STFReader stf;

        public SimisWagonReader(STFReader _stf) {
            stf = _stf;
        }

        public override void Parse(Wagon ctx) {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("uid", ()=>{ ctx.UiD = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("flip", ()=>{ stf.MustMatch("("); stf.MustMatch(")"); ctx.Flip = true; }),
                new STFReader.TokenProcessor("enginedata", ()=>{ stf.MustMatch("("); ctx.Name = stf.ReadString(); ctx.Folder = stf.ReadString(); stf.MustMatch(")"); ctx.IsEngine = true; }),
                new STFReader.TokenProcessor("wagondata", ()=>{ stf.MustMatch("("); ctx.Name = stf.ReadString(); ctx.Folder = stf.ReadString(); stf.MustMatch(")"); }),
                new STFReader.TokenProcessor("eotdata", ()=>{ stf.MustMatch("("); ctx.Name = stf.ReadString(); ctx.Folder = stf.ReadString(); stf.MustMatch(")"); ctx.IsEOT = true;  }),
                new STFReader.TokenProcessor("loaddata", ()=>
                {
                    stf.MustMatch("(");
                    if (ctx.LoadDataList == null) ctx.LoadDataList = new List<LoadData>();
                    LoadData loadData = new LoadData();
                    loadData.Name = stf.ReadString();
                    loadData.Folder = stf.ReadString();
                    var positionString = stf.ReadString();
                    Enum.TryParse(positionString, out loadData.LoadPosition);
                    var state = stf.ReadString();
                    if (state != ")")
                    {
                        Enum.TryParse(state, out loadData.LoadState);
                        ctx.LoadDataList.Add(loadData);
                        stf.MustMatch(")");
                    }
                    else
                    {
                        ctx.LoadDataList.Add(loadData);
                    }
                }),
            });
        }
    }

    public class TomlConsistReader : ConsistReader
    {
        private string consist;
        private Model consistModel;

        public TomlConsistReader(string _consist)
        {
            consist = _consist;
        }

        public override void Parse(TrainCfg ctx)
        {
            consistModel = Toml.ToModel<Model>(consist, options: new TomlModelOptions { IgnoreMissingProperties = true });

            ctx.Name = consistModel.Name;
            ctx.Serial = consistModel.Serial ?? 1;
            ctx.MaxVelocity = new MaxVelocity(MpS.From(consistModel.MaxSpeed.Value, consistModel.MaxSpeed.Unit));
            ctx.Durability = consistModel.Durability;

            foreach (var consist in consistModel.Consist)
            {
                ctx.WagonList.Add(new Wagon(new TomlWagonReader(consist)));
            }
        }
    }

    public class Model {
        public string Name { get; set; }
        public int? Serial { get; set; }
        public float Durability { get; set; }
        public ValueUnit<float, string> MaxSpeed { get; set; }
        public List<ConsistModel> Consist { get; set; }
    }

    public class ConsistModel
    {
        public bool? Flip { get; set; } = false;
        public string Type { get; set; } = "wagon";
        public string Path { get; set; }
    }

    public class TomlWagonReader : WagonReader
    {
        private ConsistModel consist;

        public TomlWagonReader(ConsistModel _consist) {
            consist = _consist;
        }

        public override void Parse(Wagon ctx)
        {
            if (consist.Flip != null) ctx.Flip = (bool)consist.Flip;

            string[] path = consist.Path.Split('/');
            ctx.Folder = path[0];
            ctx.Name = path[1];

            ctx.IsEngine = (consist.Type == "engine");
        }
    }
}
