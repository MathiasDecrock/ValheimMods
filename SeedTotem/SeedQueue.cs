using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeedTotem
{
    public class SeedQueue : IEquatable<SeedQueue> 
    {
        private readonly Queue<SeedQueue.Entry> queue = new Queue<Entry>();

        public SeedQueue() { }

        public SeedQueue(ZPackage package)
        { 
            int size = package.ReadInt();
            for (int i = 0; i < size; i++)
            {
                queue.Enqueue(new Entry(package.ReadString(), package.ReadInt()));
            } 
        }

        public void AddSeed(string name, int amount)
        {
            if(queue.Any())
            {
                Entry lastEntry = queue.Last();
                if(lastEntry.Name == name)
                {
                    lastEntry.Amount += amount;
                    return;
                }
            }
            queue.Enqueue(new Entry(name, amount));
        }

        public bool Equals(SeedQueue other)
        {
            if(queue.Count != other.queue.Count)
            {
                return false;
            }
            for (int i = 0; i < queue.Count; i++)
            {
                if(queue.ElementAt(i) != other.queue.ElementAt(i))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public int Count => queue
                .Select(entry => entry.Amount)
                .Sum();

        public string Peek()
        {
            if(queue.Count == 0)
            {
                return null;
            }
            return queue.Peek().Name;
        }

        public string Dequeue()
        {
            if(queue.Count == 0)
            {
                return null;
            }
            Entry entry = queue.Peek();
            entry.Amount--;
            if(entry.Amount <= 0)
            {
                queue.Dequeue();
            }
            return entry.Name;
        }

        public List<Entry> Restrict(string newRestrict)
        {
            Dictionary<string, int> inQueueDict = new Dictionary<string, int>();
            foreach(Entry entry in queue)
            {
                if(inQueueDict.TryGetValue(entry.Name, out int currentCount))
                {
                    inQueueDict[entry.Name] = currentCount + entry.Amount;
                } else
                {
                    inQueueDict[entry.Name] = entry.Amount;
                }
            }
            queue.Clear();
            if(inQueueDict.TryGetValue(newRestrict, out int currentRestrictCount))
            {
                queue.Enqueue(new Entry(newRestrict, currentRestrictCount));
                inQueueDict.Remove(newRestrict);
            }
            return inQueueDict
                .Select(kv => new Entry(kv.Key, kv.Value))
                .ToList();
        }

        public override int GetHashCode()
        {
            return 1833020792 + EqualityComparer<Queue<Entry>>.Default.GetHashCode(queue);
        }

        public void RemoveSeed()
        {
            if(queue.Any())
            {
                Entry entry = queue. Peek();
                entry.Amount -= 1;
                if(entry.Amount <= 0)
                {
                    queue.Dequeue();
                }
            }
        }
          
        public ZPackage ToZPackage()
        {
            var package = new ZPackage();
            package.Write(queue.Count);
            foreach (Entry entry in queue)
            {
                package.Write(entry.Name);
                package.Write(entry.Amount);
            }
            return package;
        }
          
        public static bool operator ==(SeedQueue left, SeedQueue right)
        {
            if(Object.ReferenceEquals(left, null))
            {
                return Object.ReferenceEquals(right, null);
            }
            return left.Equals(right);
        }

        public static bool operator !=(SeedQueue left, SeedQueue right)
        {
            return !(left == right);
        }

        public sealed class Entry : IEquatable<Entry>
        {
            public string Name { get; internal set; }
            public int Amount { get; internal set; }

            public Entry(string name, int amount)
            {
                this.Name = name;
                this.Amount = amount;
            }

            public bool Equals(Entry other)
            {
                return other != null &&
                       Name == other.Name &&
                       Amount == other.Amount;
            }

            public override bool Equals(object obj)
            {
                return base.Equals(obj);
            }

            public override int GetHashCode()
            {
                int hashCode = 221287967;
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
                hashCode = hashCode * -1521134295 + Amount.GetHashCode();
                return hashCode;
            }

            public override string ToString()
            {
                return base.ToString();
            }

            public static bool operator ==(Entry left, Entry right)
            {
                if (Object.ReferenceEquals(left, null))
                {
                    return Object.ReferenceEquals(right, null);
                }
                return left.Equals(right);
            }

            public static bool operator !=(Entry left, Entry right)
            {
                return !(left == right);
            }

        }
    }
}
