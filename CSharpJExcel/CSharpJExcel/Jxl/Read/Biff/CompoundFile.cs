/*********************************************************************
*
*      Copyright (C) 2002 Andrew Khan
*
* This library is free software; you can redistribute it and/or
* modify it under the terms of the GNU Lesser General Public
* License as published by the Free Software Foundation; either
* version 2.1 of the License, or (at your option) any later version.
*
* This library is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
* Lesser General Public License for more details.
*
* You should have received a copy of the GNU Lesser General Public
* License along with this library; if not, write to the Free Software
* Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
***************************************************************************/

// Port to C# 
// Chris Laforet
// Wachovia, a Wells-Fargo Company
// Feb 2010


using CSharpJExcel.Jxl.Biff;
using System.Collections;


namespace CSharpJExcel.Jxl.Read.Biff
	{
	/**
	 * Reads in and defrags an OLE compound compound file
	 * (Made public only for the PropertySets demo)
	 */
	public sealed class CompoundFile : BaseCompoundFile
		{
		/**
		 * The logger
		 */
		//private static Logger logger = Logger.getLogger(CompoundFile.class);

		/**
		 * The original OLE stream, organized into blocks, which can
		 * appear at any physical location in the file
		 */
		private byte[] data;
		/**
		 * The number of blocks it takes to store the big block depot
		 */
		private int numBigBlockDepotBlocks;
		/**
		 * The start block of the small block depot
		 */
		private int sbdStartBlock;
		/**
		 * The start block of the root entry
		 */
		private int rootStartBlock;
		/**
		 * The header extension block
		 */
		private int extensionBlock;
		/**
		 * The number of header extension blocks
		 */
		private int numExtensionBlocks;
		/**
		 * The root entry
		 */
		private byte[] rootEntry;
		/**
		 * The sequence of blocks which comprise the big block chain
		 */
		private int[] bigBlockChain;
		/**
		 * The sequence of blocks which comprise the small block chain
		 */
		private int[] smallBlockChain;
		/**
		 * The chain of blocks which comprise the big block depot
		 */
		private int[] bigBlockDepotBlocks;
		/**
		 * The list of property sets
		 */
		private ArrayList propertySets;

		/**
		 * The workbook settings
		 */
		private WorkbookSettings settings;

		/** 
		 * The property storage root entry
		 */
		private PropertyStorage rootEntryPropertyStorage;

		/**
		 * Initializes the compound file
		 *
		 * @param d the raw data of the ole stream
		 * @param ws the workbook settings
		 * @exception BiffException
		 */
		public CompoundFile(byte[] d,WorkbookSettings ws)
			: base()
			{
			data = d;
			settings = ws;

			// First verify the OLE identifier
			for (int i = 0; i < IDENTIFIER.Length; i++)
				{
				if (data[i] != IDENTIFIER[i])
					{
					throw new BiffException(BiffException.unrecognizedOLEFile);
					}
				}

			propertySets = new ArrayList();
			numBigBlockDepotBlocks = IntegerHelper.getInt
			  (data[NUM_BIG_BLOCK_DEPOT_BLOCKS_POS],
			   data[NUM_BIG_BLOCK_DEPOT_BLOCKS_POS + 1],
			   data[NUM_BIG_BLOCK_DEPOT_BLOCKS_POS + 2],
			   data[NUM_BIG_BLOCK_DEPOT_BLOCKS_POS + 3]);
			sbdStartBlock = IntegerHelper.getInt
			  (data[SMALL_BLOCK_DEPOT_BLOCK_POS],
			   data[SMALL_BLOCK_DEPOT_BLOCK_POS + 1],
			   data[SMALL_BLOCK_DEPOT_BLOCK_POS + 2],
			   data[SMALL_BLOCK_DEPOT_BLOCK_POS + 3]);
			rootStartBlock = IntegerHelper.getInt
			  (data[ROOT_START_BLOCK_POS],
			   data[ROOT_START_BLOCK_POS + 1],
			   data[ROOT_START_BLOCK_POS + 2],
			   data[ROOT_START_BLOCK_POS + 3]);
			extensionBlock = IntegerHelper.getInt
			  (data[EXTENSION_BLOCK_POS],
			   data[EXTENSION_BLOCK_POS + 1],
			   data[EXTENSION_BLOCK_POS + 2],
			   data[EXTENSION_BLOCK_POS + 3]);
			numExtensionBlocks = IntegerHelper.getInt
			  (data[NUM_EXTENSION_BLOCK_POS],
			   data[NUM_EXTENSION_BLOCK_POS + 1],
			   data[NUM_EXTENSION_BLOCK_POS + 2],
			   data[NUM_EXTENSION_BLOCK_POS + 3]);

			bigBlockDepotBlocks = new int[numBigBlockDepotBlocks];

			int pos = BIG_BLOCK_DEPOT_BLOCKS_POS;

			int bbdBlocks = numBigBlockDepotBlocks;

			if (numExtensionBlocks != 0)
				{
				bbdBlocks = (BIG_BLOCK_SIZE - BIG_BLOCK_DEPOT_BLOCKS_POS) / 4;
				}

			for (int i = 0; i < bbdBlocks; i++)
				{
				bigBlockDepotBlocks[i] = IntegerHelper.getInt
				  (d[pos],d[pos + 1],d[pos + 2],d[pos + 3]);
				pos += 4;
				}

			for (int j = 0; j < numExtensionBlocks; j++)
				{
				pos = (extensionBlock + 1) * BIG_BLOCK_SIZE;
				int blocksToRead = System.Math.Min(numBigBlockDepotBlocks - bbdBlocks,BIG_BLOCK_SIZE / 4 - 1);

				for (int i = bbdBlocks; i < bbdBlocks + blocksToRead; i++)
					{
					bigBlockDepotBlocks[i] = IntegerHelper.getInt
					  (d[pos],d[pos + 1],d[pos + 2],d[pos + 3]);
					pos += 4;
					}

				bbdBlocks += blocksToRead;
				if (bbdBlocks < numBigBlockDepotBlocks)
					{
					extensionBlock = IntegerHelper.getInt
					  (d[pos],d[pos + 1],d[pos + 2],d[pos + 3]);
					}
				}

			readBigBlockDepot();
			readSmallBlockDepot();

			rootEntry = readData(rootStartBlock);
			readPropertySets();
			}

		/**
		 * Reads the big block depot entries
		 */
		private void readBigBlockDepot()
			{
			int pos = 0;
			int index = 0;
			bigBlockChain = new int[numBigBlockDepotBlocks * BIG_BLOCK_SIZE / 4];

			for (int i = 0; i < numBigBlockDepotBlocks; i++)
				{
				pos = (bigBlockDepotBlocks[i] + 1) * BIG_BLOCK_SIZE;

				for (int j = 0; j < BIG_BLOCK_SIZE / 4; j++)
					{
					bigBlockChain[index] = IntegerHelper.getInt
					  (data[pos],data[pos + 1],data[pos + 2],data[pos + 3]);
					pos += 4;
					index++;
					}
				}
			}

		/**
		 * Reads the small block chain's depot entries
		 */
		private void readSmallBlockDepot()
			{
			int pos = 0;
			int index = 0;
			int sbdBlock = sbdStartBlock;
			smallBlockChain = new int[0];

			// Some non-excel generators specify -1 for an empty small block depot
			// simply warn and return
			if (sbdBlock == -1)
				{
				//logger.warn("invalid small block depot number");
				return;
				}

			int blockCount = 0;
			for (; blockCount <= bigBlockChain.Length && sbdBlock != -2; blockCount++)
				{
				// Allocate some more space to the small block chain
				int[] oldChain = smallBlockChain;
				smallBlockChain = new int[smallBlockChain.Length + BIG_BLOCK_SIZE / 4];
				System.Array.Copy(oldChain,0,smallBlockChain,0,oldChain.Length);

				pos = (sbdBlock + 1) * BIG_BLOCK_SIZE;

				for (int j = 0; j < BIG_BLOCK_SIZE / 4; j++)
					{
					smallBlockChain[index] = IntegerHelper.getInt
					  (data[pos],data[pos + 1],data[pos + 2],data[pos + 3]);
					pos += 4;
					index++;
					}

				sbdBlock = bigBlockChain[sbdBlock];
				}

			if (blockCount > bigBlockChain.Length)
				{
				// Attempted to read more blocks than the block chain contains entries 
				// for.  This indicates a loop in the chain
				throw new BiffException(BiffException.corruptFileFormat);
				}
			}

		/**
		 * Reads all the property sets
		 */
		private void readPropertySets()
			{
			int offset = 0;
			byte[] d = null;

			while (offset < rootEntry.Length)
				{
				d = new byte[PROPERTY_STORAGE_BLOCK_SIZE];
				System.Array.Copy(rootEntry,offset,d,0,d.Length);
				PropertyStorage ps = new PropertyStorage(d);

				// sometimes the MAC Operating system leaves some property storage
				// names blank.  Contributed by Jacky
				if (ps.name == null || ps.name.Length == 0)
					{
					if (ps.type == ROOT_ENTRY_PS_TYPE)
						{
						ps.name = ROOT_ENTRY_NAME;
						//logger.warn("Property storage name for " + ps.type +
						//            " is empty - setting to " + ROOT_ENTRY_NAME);
						}
					else
						{
						if (ps.size != 0)
							{
							//logger.warn("Property storage type " + ps.type +
							//            " is non-empty and has no associated name");
							}
						}
					}
				propertySets.Add(ps);
				if (System.String.Compare(ps.name,ROOT_ENTRY_NAME,true) == 0)
					rootEntryPropertyStorage = ps;
				offset += PROPERTY_STORAGE_BLOCK_SIZE;
				}

			if (rootEntryPropertyStorage == null)
				rootEntryPropertyStorage = (PropertyStorage)propertySets[0];
			}

		/**
		 * Gets the defragmented stream from this ole compound file
		 *
		 * @param streamName the stream name to get
		 * @return the defragmented ole stream
		 * @exception BiffException
		 */
		public byte[] getStream(string streamName)
			{
			PropertyStorage ps = findPropertyStorage(streamName,
													 rootEntryPropertyStorage);

			// Property set can't be found from the direct hierarchy, so just
			// search on the name
			if (ps == null)
				{
				ps = getPropertyStorage(streamName);
				}

			if (ps.size >= SMALL_BLOCK_THRESHOLD ||
				System.String.Compare(streamName,ROOT_ENTRY_NAME) == 0)
				{
				return getBigBlockStream(ps);
				}
			else
				{
				return getSmallBlockStream(ps);
				}
			}

		/**
		 * Gets the defragmented stream from this ole compound file.  Used when
		 * copying workbooks with macros
		 *
		 * @param psIndex the property storage index
		 * @return the defragmented ole stream
		 * @exception BiffException
		 */
		public byte[] getStream(int psIndex)
			{
			PropertyStorage ps = getPropertyStorage(psIndex);

			if (ps.size >= SMALL_BLOCK_THRESHOLD ||
				System.String.Compare(ps.name,ROOT_ENTRY_NAME) == 0)
				{
				return getBigBlockStream(ps);
				}
			else
				{
				return getSmallBlockStream(ps);
				}
			}

		/**
		 * Recursively searches the property storages in hierarchy order 
		 * for the appropriate name.  This is the public version which is
		 * invoked from the writable version
		 * when copying a sheet with addition property sets.
		 */
		public PropertyStorage findPropertyStorage(string name)
			{
			return findPropertyStorage(name,rootEntryPropertyStorage);
			}

		/**
		 * Recursively searches the property storages in hierarchy order 
		 * for the appropriate name.
		 */
		private PropertyStorage findPropertyStorage(string name,
												   PropertyStorage ps)
			{
			if (ps.child == -1)
				{
				return null;
				}

			// Get the child
			PropertyStorage child = getPropertyStorage(ps.child);
			if (System.String.Compare(child.name,name) == 0)
				return child;

			// Find the previous property storages on the same level
			PropertyStorage prev = child;
			while (prev.previous != -1)
				{
				prev = getPropertyStorage(prev.previous);
				if (System.String.Compare(prev.name,name) == 0)
					return prev;
				}

			// Find the next property storages on the same level
			PropertyStorage next = child;
			while (next.next != -1)
				{
				next = getPropertyStorage(next.next);
				if (System.String.Compare(next.name,name) == 0)
					return next;
				}

			return findPropertyStorage(name,child);
			}

		/**
		 * Gets the property set with the specified name
		 * @param name the property storage name
		 * @return the property storage record
		 * @exception BiffException
		 * @deprecated remove me
		 */
		private PropertyStorage getPropertyStorage(string name)
			{
			// Find the workbook property
			bool found = false;
			bool multiple = false;
			PropertyStorage ps = null;
			foreach (PropertyStorage ps2 in propertySets)
				{
				if (System.String.Compare(ps2.name,name,true) == 0)
					{
					multiple = found == true ? true : false;
					found = true;
					ps = ps2;
					}
				}

			if (multiple)
				{
				//logger.warn("found multiple copies of property set " + name);
				}

			if (!found)
				{
				throw new BiffException(BiffException.streamNotFound);
				}

			return ps;
			}

		/**
		 * Gets the property set with the specified name
		 * @param index the index of the property storage
		 * @return the property storage record
		 */
		private PropertyStorage getPropertyStorage(int index)
			{
			return (PropertyStorage)propertySets[index];
			}

		/**
		 * Build up the resultant stream using the big blocks
		 *
		 * @param ps the property storage
		 * @return the big block stream
		 */
		private byte[] getBigBlockStream(PropertyStorage ps)
			{
			int numBlocks = ps.size / BIG_BLOCK_SIZE;
			if (ps.size % BIG_BLOCK_SIZE != 0)
				{
				numBlocks++;
				}

			byte[] streamData = new byte[numBlocks * BIG_BLOCK_SIZE];

			int block = ps.startBlock;

			int count = 0;
			int pos = 0;
			while (block != -2 && count < numBlocks)
				{
				pos = (block + 1) * BIG_BLOCK_SIZE;
				System.Array.Copy(data,pos,streamData,
								 count * BIG_BLOCK_SIZE,BIG_BLOCK_SIZE);
				count++;
				block = bigBlockChain[block];
				}

			if (block != -2 && count == numBlocks)
				{
				//logger.warn("Property storage size inconsistent with block chain.");
				}

			return streamData;
			}

		/**
		 * Build up the resultant stream using the small blocks
		 * @param ps the property storage
		 * @return  the data
		 * @exception BiffException
		 */
		private byte[] getSmallBlockStream(PropertyStorage ps)
			{
			byte[] rootdata = readData(rootEntryPropertyStorage.startBlock);
			byte[] sbdata = new byte[0];

			int block = ps.startBlock;
			int pos = 0;

			int blockCount = 0;
			for (; blockCount <= smallBlockChain.Length && block != -2; blockCount++)
				{
				// grow the array
				byte[] olddata = sbdata;
				sbdata = new byte[olddata.Length + SMALL_BLOCK_SIZE];
				System.Array.Copy(olddata,0,sbdata,0,olddata.Length);

				// Copy in the new data
				pos = block * SMALL_BLOCK_SIZE;
				System.Array.Copy(rootdata,pos,sbdata,
								 olddata.Length,SMALL_BLOCK_SIZE);
				block = smallBlockChain[block];

				if (block == -1)
					{
					//logger.warn("Incorrect terminator for small block stream " + ps.name);
					block = -2; // kludge to force the loop termination
					}
				}

			if (blockCount > smallBlockChain.Length)
				{
				// Attempted to read more blocks than the block chain contains entries 
				// for. This indicates a loop in the chain
				throw new BiffException(BiffException.corruptFileFormat);
				}

			return sbdata;
			}

		/**
		 * Reads the block chain from the specified block and returns the
		 * data as a continuous stream of bytes
		 *
		 * @param bl the block number
		 * @return the data
		 */
		private byte[] readData(int bl)
			{
			int block = bl;
			int pos = 0;
			byte[] entry = new byte[0];

			int blockCount = 0;
			for (; blockCount <= bigBlockChain.Length && block != -2; blockCount++)
				{
				// Grow the array
				byte[] oldEntry = entry;
				entry = new byte[oldEntry.Length + BIG_BLOCK_SIZE];
				System.Array.Copy(oldEntry,0,entry,0,oldEntry.Length);
				pos = (block + 1) * BIG_BLOCK_SIZE;
				System.Array.Copy(data,pos,entry,
								 oldEntry.Length,BIG_BLOCK_SIZE);
				if (bigBlockChain[block] == block)
					{
					throw new BiffException(BiffException.corruptFileFormat);
					}
				block = bigBlockChain[block];
				}

			if (blockCount > bigBlockChain.Length)
				{
				// Attempted to read more blocks than the block chain contains entries 
				// for.  This indicates a loop in the chain
				throw new BiffException(BiffException.corruptFileFormat);
				}

			return entry;
			}

		/**
		 * Gets the number of property sets
		 * @return the number of property sets
		 */
		public int getNumberOfPropertySets()
			{
			return propertySets.Count;
			}

		/**
		 * Gets the property set.  Invoked when copying worksheets with macros.  
		 * Simply calls the private counterpart
		 *
		 * @param ps the property set name
		 * @return the property set with the given name
		 */
		public PropertyStorage getPropertySet(int index)
			{
			return getPropertyStorage(index);
			}
		}
	}












