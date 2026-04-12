namespace UniMediator.Runtime
{
    public interface IMediator : ISender, IPublisher 
        #if UNIMEDIATOR_UNITASK_INTEGRATION 
        , IAsyncPublisher, IStreamSender
        #endif
    {
    }
}