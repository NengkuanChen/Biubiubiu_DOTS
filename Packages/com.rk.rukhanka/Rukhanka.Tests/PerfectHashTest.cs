using NUnit.Framework;
using Rukhanka;
using Unity.Collections;
using Hash128 = Unity.Entities.Hash128;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Tests
{ 
public class PerfectHashTest
{
    [Test]
    public void InternalHashFuncTest()
    {
		var testHash = new Hash128(0x12345678, 0xabcddcba, 0x1a2b3c4d, 0x11223344);

		var hashArr = new NativeList<Hash128>(Allocator.Temp)
		{
			testHash,
			new Hash128(testHash.Value.xzyx),
			new Hash128(testHash.Value.yyzz),
			new Hash128(testHash.Value.yxyx),
			new Hash128(testHash.Value.wzyx),
			new Hash128(testHash.Value.wxyz),
			new Hash128(testHash.Value.wwyy),
			new Hash128(testHash.Value.xxyy),
			new Hash128(testHash.Value.xxww),
			new Hash128(testHash.Value.xxxx),
			new Hash128(testHash.Value.yyyy),
			new Hash128(testHash.Value.zzzz),
			new Hash128(testHash.Value.wwww),
		};
		PerfectHash.CreateMinimalPerfectHash(hashArr.AsArray(), out var seedArr, out var shuffleArr);

		uint posFlags = 0;
		for (int i = 0; i < shuffleArr.Length; ++i)
		{
			var sv = shuffleArr[i];
			var bitMask = 1u << sv;
			Assert.IsTrue((posFlags & bitMask) == 0);
			posFlags |= bitMask;
		}

		for (int i = 0; i < hashArr.Length; ++i)
		{
			var iHash = hashArr[i];
			var l = PerfectHash.QueryPerfectHashTable(seedArr, iHash);
			Assert.IsTrue(hashArr[shuffleArr[l]] == iHash);
		}	
    }
}
}
