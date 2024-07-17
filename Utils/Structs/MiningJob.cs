using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Structs
{
    [Serializable]
    public struct MiningJob
    {
        public Block candidateBlock;
        public int currentDifficulty;

        public MiningJob(Block candidateBlock, int currentDifficulty)
        {
            this.candidateBlock = candidateBlock;
            this.currentDifficulty = currentDifficulty;
        }
    }
}
