using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ow.Net;
class ClientPacketBufferRegression {
    static readonly byte[] First={0,6,0x42,0xa7,1,2,3,4};
    static readonly byte[] Second={0,4,0x15,0x7f,5,6};
    static void Check(bool value){if(!value)throw new Exception("TCP framing regression");}
    static int Main(){
        var combined=First.Concat(Second).ToArray();
        for(int cut=0;cut<=combined.Length;cut++){
            var buffer=new ClientPacketBuffer();var frames=new List<byte[]>();
            buffer.Feed(combined.Take(cut).ToArray(),cut,frames.Add,()=>{});
            buffer.Feed(combined.Skip(cut).ToArray(),combined.Length-cut,frames.Add,()=>{});
            Check(frames.Count==2&&frames[0].SequenceEqual(First)&&frames[1].SequenceEqual(Second));
        }
        var each=new ClientPacketBuffer();int calls=0,policies=0;
        foreach(byte b in Encoding.ASCII.GetBytes("<policy-file-request/>\0").Concat(combined))each.Feed(new[]{b},1,_=>calls++,()=>policies++);
        Check(calls==2&&policies==1);
        var batch=new ClientPacketBuffer();var many=Enumerable.Range(0,500).SelectMany(_=>First).ToArray();calls=0;
        batch.Feed(many,many.Length,_=>calls++,()=>{});Check(calls==500);
        var large=new byte[0x3c70+2];large[0]=0x3c;large[1]=0x70;large[2]=1;calls=0;
        new ClientPacketBuffer().Feed(large,large.Length,_=>calls++,()=>{throw new Exception("Binary length mistaken for policy");});Check(calls==1);
        try{new ClientPacketBuffer().Feed(new byte[]{0,1},2,_=>{},()=>{});throw new Exception("Invalid length accepted");}catch(InvalidDataException){}
        Console.WriteLine("TCP coalescing, every split boundary, bytewise delivery, policy handshake and invalid lengths passed");return 0;
    }
}
