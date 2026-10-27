namespace Aurora.Modules.Sync;

// One scoped runner per tenant/connection. Hosts decide scheduling; the core
// never discovers credentials or depends on a particular external database.
public sealed class SyncRunner(SyncStore store,ISyncProvider provider,IExternalChangeImporter importer)
{
    public async Task<int> PushPending(Guid connectionId,CancellationToken ct)
    {
        var completed=0;
        for(var i=0;i<100;i++)
        {
            ct.ThrowIfCancellationRequested();
            var change=await store.Claim(connectionId,ct);
            if(change is null)break;
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));
            try
            {
                var receipt=await provider.Push(change,timeout.Token);
                await store.Complete(change,receipt,ct);
                completed++;
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
            catch(SyncConflictException ex){await store.Retry(change,true,ct,ex.Message);}
            catch(FormatException ex){await store.Retry(change,true,ct,ex.Message);}
            catch(Exception){await store.Retry(change,false,ct);}
        }
        return completed;
    }

    public async Task Pull(Guid connectionId,string? checkpoint,CancellationToken ct)
    {
        var batch=await provider.Pull(checkpoint,ct);
        // One native aggregate can expand into many dependencies. Commit bounded
        // chunks, but acknowledge native events only after every chunk succeeds.
        // On retry, committed chunks are deduplicated by their inbox receipts.
        if(batch.Changes.Count==0)await importer.Apply(connectionId,batch,ct);
        else
        {
            // Commit dependencies before a manifest can encounter a pending local
            // order edit. Its conflict must remain visible even if membership waits.
            var pieces=new List<ExternalChange[]>();
            var pending=new List<ExternalChange>();
            foreach(var change in batch.Changes)
            {
                if(change.EntityType=="manifest")
                {
                    if(pending.Count>0){pieces.Add(pending.ToArray());pending.Clear();}
                    pieces.Add([change]);
                }
                else
                {
                    pending.Add(change);
                    if(pending.Count==500){pieces.Add(pending.ToArray());pending.Clear();}
                }
            }
            if(pending.Count>0)pieces.Add(pending.ToArray());
            var chunks=pieces.ToArray();
            for(var i=0;i<chunks.Length;i++)
                await importer.Apply(connectionId,new ExternalBatch(chunks[i],i==chunks.Length-1?batch.Checkpoint:checkpoint),ct);
        }
        if(provider is ISyncAcknowledgement acknowledgement)
            await acknowledgement.Acknowledge(batch,ct);
    }
}
