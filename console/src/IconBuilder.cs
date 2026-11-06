using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// Packages the supplied PNG into a multi-size Windows icon.
class IconBuilder {
 static void Main(string[] args) {
  if(args.Length!=1)throw new ArgumentException("Pass the console Assets folder.");
  string source=Path.Combine(args[0],"FlightControl.png");
  string target=Path.Combine(args[0],"FlightControl.ico");
  int[] sizes={16,24,32,48,64,128,256};
  var images=new byte[sizes.Length][];
  using(var original=Image.FromFile(source)) {
   for(int i=0;i<sizes.Length;i++)using(var bitmap=new Bitmap(sizes[i],sizes[i],PixelFormat.Format32bppArgb)) {
    using(var g=Graphics.FromImage(bitmap)) {
     g.Clear(Color.Transparent);
     g.CompositingMode=CompositingMode.SourceCopy;
     g.InterpolationMode=InterpolationMode.HighQualityBicubic;
     g.SmoothingMode=SmoothingMode.HighQuality;
     g.PixelOffsetMode=PixelOffsetMode.HighQuality;
     g.DrawImage(original,new Rectangle(0,0,sizes[i],sizes[i]));
    }
    using(var stream=new MemoryStream()) {bitmap.Save(stream,ImageFormat.Png);images[i]=stream.ToArray();}
   }
  }
  using(var writer=new BinaryWriter(File.Create(target))) {
   writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);
   int offset=6+16*sizes.Length;
   for(int i=0;i<sizes.Length;i++) {
    writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));
    writer.Write((byte)0);writer.Write((byte)0);
    writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(images[i].Length);
    writer.Write(offset);offset+=images[i].Length;
   }
   foreach(var bytes in images)writer.Write(bytes);
  }
 }
}
