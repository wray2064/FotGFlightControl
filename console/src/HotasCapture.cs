using System;
using System.Collections.Generic;
using System.Linq;

// What the player moved or pressed while Flight Control was listening.
sealed class CaptureCandidate {
 public int Device;
 public string Kind;      // Button, Hat or Axis
 public int Index;
 public int Direction;    // hat bits, or the +1/-1 side an axis moved towards
 public int Peak;         // how far an axis travelled
 public bool Ambiguous;   // another axis moved nearly as much
}

// Watches every input on every device and works out which single control the player used.
// Fed once per tick with each input's raw value; the first reading of an input is its resting state.
sealed class InputCapture {
 internal enum Mode{Axis,Digital,Any}
 const int NoiseTicks=8,NoiseLimit=1500,AxisThreshold=4096,SideThreshold=16000;

 sealed class Track{
  public int Device,Index,Baseline,Min,Max,Last,FirstChange=-1;
  public string Kind;
  public bool Seen;
 }

 readonly Mode mode;
 readonly bool axisSides;
 readonly Dictionary<string,Track> tracks=new Dictionary<string,Track>();
 int tick,changeCounter;

 // axisSides lets an axis stand in for a button (a throttle lever used as a switch, for example).
 public InputCapture(Mode mode,bool axisSides){this.mode=mode;this.axisSides=axisSides;}

 public void NextTick(){tick++;}

 public void Feed(int device,string kind,int index,int raw) {
  string key=device+":"+kind+index;
  Track t;
  if(!tracks.TryGetValue(key,out t)){
   t=new Track{Device=device,Kind=kind,Index=index,Baseline=raw,Min=raw,Max=raw,Last=raw,Seen=true};
   tracks[key]=t;
   return;
  }
  t.Last=raw;
  t.Min=Math.Min(t.Min,raw);
  t.Max=Math.Max(t.Max,raw);
  if(kind!="Axis"&&raw!=t.Baseline&&(kind=="Button"||raw!=0)&&t.FirstChange<0)t.FirstChange=changeCounter++;
 }

 // Movement of an axis away from where it rested, ignoring the first moments so a jittery axis is judged by its noise.
 int Travel(Track t,out int direction) {
  int up=t.Max-t.Baseline,down=t.Baseline-t.Min;
  direction=up>=down?1:-1;
  return Math.Max(up,down);
 }

 // Axes that wander on their own while the player is still (a worn pot, a noisy throttle) are ignored.
 readonly Dictionary<string,int> noise=new Dictionary<string,int>();
 public void Settle() {
  if(tick!=NoiseTicks)return;
  foreach(var pair in tracks)if(pair.Value.Kind=="Axis")noise[pair.Key]=pair.Value.Max-pair.Value.Min;
  // Whatever moved before this point is jitter, not intent: start measuring from the settled value.
  foreach(var pair in tracks)if(pair.Value.Kind=="Axis"){pair.Value.Baseline=pair.Value.Last;pair.Value.Min=pair.Value.Max=pair.Value.Last;}
 }

 public CaptureCandidate Best() {
  if(tick<NoiseTicks)return null;
  Track button=null,hat=null;
  if(mode!=Mode.Axis){
   button=tracks.Values.Where(t=>t.Kind=="Button"&&t.FirstChange>=0).OrderBy(t=>t.FirstChange).FirstOrDefault();
   hat=tracks.Values.Where(t=>t.Kind=="Hat"&&t.FirstChange>=0&&t.Last!=0).OrderBy(t=>t.FirstChange).FirstOrDefault();
  }
  if(button!=null&&(hat==null||button.FirstChange<hat.FirstChange))
   return new CaptureCandidate{Device=button.Device,Kind="Button",Index=button.Index};
  if(hat!=null)return new CaptureCandidate{Device=hat.Device,Kind="Hat",Index=hat.Index,Direction=hat.Last&15};
  if(mode==Mode.Digital&&!axisSides)return null;
  int need=mode==Mode.Digital?SideThreshold:AxisThreshold;
  var moved=new List<CaptureCandidate>();
  foreach(var pair in tracks){
   var t=pair.Value;
   if(t.Kind!="Axis")continue;
   int ignored;
   if(noise.TryGetValue(pair.Key,out ignored)&&ignored>NoiseLimit)continue;
   int direction;
   int travel=Travel(t,out direction);
   if(travel>=need)moved.Add(new CaptureCandidate{Device=t.Device,Kind="Axis",Index=t.Index,Direction=direction,Peak=travel});
  }
  if(moved.Count==0)return null;
  var ranked=moved.OrderByDescending(c=>c.Peak).ToList();
  var best=ranked[0];
  // A second axis that moved almost as far means the player moved two controls; wait for a clear winner.
  best.Ambiguous=ranked.Count>1&&ranked[1].Peak*10>best.Peak*6&&best.Peak<60000;
  return best;
 }

 internal static bool SelfTest() {
  // A button press on the second device wins; the first device's quiet inputs do not.
  var c=new InputCapture(Mode.Digital,true);
  for(int i=0;i<12;i++){
   c.NextTick();
   c.Feed(0,"Button",0,0);c.Feed(0,"Axis",1,100+(i%2));c.Feed(1,"Button",4,i>=9?1:0);
   c.Settle();
  }
  var press=c.Best();
  if(press==null||press.Device!=1||press.Kind!="Button"||press.Index!=4)return false;

  // The strongest axis wins, but two similar movements are ambiguous.
  c=new InputCapture(Mode.Axis,false);
  for(int i=0;i<14;i++){
   c.NextTick();
   c.Feed(0,"Axis",0,i>=10?-30000:0);c.Feed(0,"Axis",1,i>=10?3000:0);c.Feed(0,"Button",2,i>=10?1:0);
   c.Settle();
  }
  var axis=c.Best();
  if(axis==null||axis.Index!=0||axis.Direction!=-1||axis.Ambiguous)return false;
  c=new InputCapture(Mode.Axis,false);
  for(int i=0;i<14;i++){
   c.NextTick();
   c.Feed(0,"Axis",0,i>=10?20000:0);c.Feed(0,"Axis",1,i>=10?18000:0);
   c.Settle();
  }
  if(!c.Best().Ambiguous)return false;

  // A jittery axis is ignored; a throttle that rests at one end reports the side it moved towards.
  c=new InputCapture(Mode.Axis,false);
  for(int i=0;i<20;i++){
   c.NextTick();
   c.Feed(0,"Axis",0,i%2==0?-8000:8000);
   c.Feed(0,"Axis",1,-32768+(i>=12?20000:0));
   c.Settle();
  }
  var slider=c.Best();
  if(slider==null||slider.Index!=1||slider.Direction!=1)return false;

  // A hat reports its direction bits; a digital capture will not take a small axis wobble.
  c=new InputCapture(Mode.Digital,true);
  for(int i=0;i<12;i++){
   c.NextTick();
   c.Feed(0,"Hat",0,i>=10?HotasModel.HatUp:0);c.Feed(0,"Axis",0,i>=10?5000:0);
   c.Settle();
  }
  var hat=c.Best();
  if(hat==null||hat.Kind!="Hat"||hat.Direction!=HotasModel.HatUp)return false;
  c=new InputCapture(Mode.Digital,true);
  for(int i=0;i<12;i++){c.NextTick();c.Feed(0,"Axis",0,i>=10?5000:0);c.Settle();}
  if(c.Best()!=null)return false;
  // Nothing is reported before the quiet period ends.
  c=new InputCapture(Mode.Any,true);
  c.NextTick();c.Feed(0,"Button",0,0);c.NextTick();c.Feed(0,"Button",0,1);
  return c.Best()==null;
 }
}
