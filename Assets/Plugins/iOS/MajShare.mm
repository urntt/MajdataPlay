#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import "UnityInterface.h"

// 分享成绩：弹出系统分享菜单。paths 为以换行分隔的文件路径，text 为要分享的文字（可为空）
extern "C" void MajShare_Share(const char* paths, const char* text)
{
    NSString* pathList = paths ? [NSString stringWithUTF8String:paths] : @"";
    NSString* shareText = text ? [NSString stringWithUTF8String:text] : @"";
    NSMutableArray* items = [NSMutableArray array];
    for (NSString* path in [pathList componentsSeparatedByString:@"\n"])
    {
        if (path.length > 0)
        {
            [items addObject:[NSURL fileURLWithPath:path]];
        }
    }
    if (shareText.length > 0)
    {
        [items addObject:shareText];
    }
    if (items.count == 0)
    {
        return;
    }
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController* root = UnityGetGLViewController();
        if (root == nil)
        {
            return;
        }
        UIActivityViewController* controller = [[UIActivityViewController alloc] initWithActivityItems:items
                                                                                 applicationActivities:nil];
        // iPad 上必须指定弹出位置，否则会闪退：放在屏幕中央，不显示箭头
        UIPopoverPresentationController* popover = controller.popoverPresentationController;
        if (popover != nil)
        {
            popover.sourceView = root.view;
            popover.sourceRect = CGRectMake(CGRectGetMidX(root.view.bounds), CGRectGetMidY(root.view.bounds), 1, 1);
            popover.permittedArrowDirections = 0;
        }
        [root presentViewController:controller animated:YES completion:nil];
    });
}
